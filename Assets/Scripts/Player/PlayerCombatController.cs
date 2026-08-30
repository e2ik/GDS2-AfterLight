using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.VisualScripting;

public enum ParryDirection
{
    Up,
    Down,
    Left,
    Right
}

public class PlayerCombatController : MonoBehaviour
{
    [Header("Parry Settings")]
    // actual parry window duration
    [SerializeField] private float parryActiveDuration = 0.2f;

    // if we want to implement this to parry bad timing
    [SerializeField] private float parryRecoveryDuration = 0.3f;

    // how long a parry button press stays queued in memory if pressed early
    [SerializeField] private float parryBufferTime = 0.15f;
    
    private float parryActiveTimer;
    private float parryRecoveryTimer;
    private float parryBufferTimer;

    private bool isParrying;           // True during Active Window only
    private bool isParryInRecovery;    // True during Recovery Window only
    private ParryDirection parryDir;

    [Header("Attack Settings")]
    [SerializeField] private Transform attackOrigin;
    public LayerMask enemyLayer;
    [SerializeField] private float critDamageMultiplier = 2f;

    [SerializeField] private float counterAttackMultiplier = 1.2f;
    [SerializeField] private float attackWidth = 1.5f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackCoolDown = 0.2f;
    [SerializeField] private float counterAttackWindow = 0.5f;

    private float attackBufferTimer;
    private float attackBufferTime => parryBufferTime;
    private Vector2 attackDir;
    private Vector2 attackRange;
    private Vector2 attackCenter;
    private float attackDamage;
    private float attackCrit;
    private float attackTimer;
    private bool attackPressed;
    private bool isAttacking;
    private bool isCounterAttacking;

    [Header("Skill Settings")]
    [SerializeField] private bool skillMeterAlwaysFull;
    [SerializeField] private float skillCoolDown = 0.2f;
    [SerializeField] private float skillDamageTick = 0.33f;
    private float skillBufferTimer;
    private float skillBufferTime => attackBufferTime;
    private float skillTimer;
    private bool skillPressed;
    private bool skillReleased;
    private bool isSkilling;
    [Range(0f, 1f)] public float SkillMeter { get; private set; }
    [SerializeField] private float chargeSkillAmount = 0.2f;
    private bool skillReady;

    private float verticalInput;
    private Player Player;

    public bool IsAttacking => isAttacking;
    // just for now we can make visual distinction later
    public bool IsParrying => isParrying || isParryInRecovery || isParrySuccess;
    public bool IsSkilling => isSkilling;
    public string CurrentSkillGemName { get; private set; }
    // parry anim sorry I can't control this cleanly in Anim script
    [SerializeField] private float successfulParryVisualDuration = 0.15f;
    private bool isParrySuccess;
    private Coroutine parrySuccessResetCoroutine;
    private Coroutine skillCoroutine;

    // UI additions
    public event Action<float, float> OnEnergyChanged;
    

    private void Awake()
    {
        Player = GetComponentInParent<Player>();
    }

    private void Start()
    {
        OnEnergyChanged?.Invoke(SkillMeter, 1f);
    }

    private void Update()
    {
        if (!Player.Controller.InputEnabled) return;

        HandleParry();
        HandleAttack();
        HandleSkill();
        UpdateTimers();
    }

    private void UpdateTimers()
    {
        // Countdown Input Buffer
        if (parryBufferTimer > 0f)
            parryBufferTimer -= Time.deltaTime;
        if (attackBufferTimer > 0f)
            attackBufferTimer -= Time.deltaTime;
        if (skillBufferTimer > 0f)
            skillBufferTimer -= Time.deltaTime;
        
        if (isAttacking)
            attackTimer = attackCoolDown;
        else
            attackTimer -= Time.deltaTime;
        
        if (isSkilling) 
            skillTimer = skillCoolDown;
        else 
            skillTimer -= Time.deltaTime;

        // Active Parry Window
        if (isParrying)
        {
            parryActiveTimer -= Time.deltaTime;
            if (parryActiveTimer <= 0f)
            {
                isParrying = false;
                isParryInRecovery = true;
                parryRecoveryTimer = parryRecoveryDuration;
            }
        }

        // Parry Recovery Window
        if (isParryInRecovery)
        {
            parryRecoveryTimer -= Time.deltaTime;
            if (parryRecoveryTimer <= 0f)
            {
                isParryInRecovery = false;
                
                // Unfreeze player movement when recovery finishes
                if (Player.Controller != null)
                    Player.Controller.FreezeMovement(false);
            }
        }
    }

    private bool CanAct()
    {
        return Player.Controller.InputEnabled 
            && !Player.Controller.IsWallSliding 
            && !Player.Controller.IsChargingSkill 
            && !isParrying 
            && !isParryInRecovery 
            && !isAttacking;
    }

    #region Parry Logic

    private void HandleParry()
    {
        if (parryBufferTimer > 0f && CanAct())
        {
            ExecuteParry();
        }
    }

    private void ExecuteParry()
    {
        parryBufferTimer = 0f;
        isParrying = true;
        isParryInRecovery = false;
        parryActiveTimer = parryActiveDuration;
        
        if (Player.Controller != null) 
            Player.Controller.FreezeMovement(true);

        parryDir = GetInputDirection();

        Debug.Log($"Parry Executed! Direction: {parryDir}");
    }

    private ParryDirection GetInputDirection()
    {
        ParryDirection inputDir = Player.Controller.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;
        if (verticalInput > 0.01f) 
            inputDir = ParryDirection.Up;
        else if (verticalInput < -0.01f && !Player.Controller.IsGrounded) 
            inputDir = ParryDirection.Down;
        return inputDir;
    }

    public bool CheckParry(ParryDirection incomingDirection)
    {
        if (isParrying && parryDir == incomingDirection)
        {
            OnSuccessfulParry();
            return true;
        }
        return false;
    }

    private void OnSuccessfulParry()
    {
        Player.Animation.FlashGreenOnParrySuccess();

        isParrying = false;
        isParryInRecovery = false;
        parryActiveTimer = 0f;
        parryRecoveryTimer = 0f;
        
        ChargeSkillMeter(chargeSkillAmount);

        if (Player.Controller != null)
            Player.Controller.FreezeMovement(false);

        isCounterAttacking = true;
        CancelInvoke(nameof(EndCounterAttackWindow));
        Invoke(nameof(EndCounterAttackWindow), counterAttackWindow);
    
        // Start Coroutine to set and hold isParrySuccess
        if (parrySuccessResetCoroutine != null)
            StopCoroutine(parrySuccessResetCoroutine);

        parrySuccessResetCoroutine = StartCoroutine(ExtendParryAnim(successfulParryVisualDuration));

        Debug.Log("Successful Parry!");
    }

    private IEnumerator ExtendParryAnim(float delay)
    {
        isParrySuccess = true;
        yield return new WaitForSeconds(delay);
        isParrySuccess = false;
        parrySuccessResetCoroutine = null;
    }

    private void EndCounterAttackWindow()
    {
        isCounterAttacking = false;
    }

    public void CancelParry()
    {
        // Stop lingering success animation coroutine
        if (parrySuccessResetCoroutine != null)
        {
            StopCoroutine(parrySuccessResetCoroutine);
            parrySuccessResetCoroutine = null;
        }

        isParrying = false;
        isParryInRecovery = false;
        isParrySuccess = false;
        parryActiveTimer = 0f;
        parryRecoveryTimer = 0f;

        // Reset movement freeze and gravity state
        if (Player.Controller != null)
        {
            Player.Controller.FreezeMovement(false);
            // Player.Controller.SetParryGravity(false);
        }
    }

    #endregion

    #region Attack Logic

    private void HandleAttack()
    {
        
        if (attackBufferTimer > 0f && CanAct())
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        attackBufferTimer = 0f;

        if (attackPressed && attackTimer <= 0f && CanAct())
        {
            CancelParry();

            attackPressed = false;
            
            if (Player.Equipment.EquippedWeapon == null)
            {
                Debug.LogWarning("[PlayerCombatController] Cannot attack: No weapon equipped.");
                attackPressed = false;
                return;
            }

            isAttacking = true;

            attackDir = GetInputDirection() switch
            {
                ParryDirection.Up => Vector2.up,
                ParryDirection.Down => Vector2.down,
                ParryDirection.Left => Vector2.left,
                ParryDirection.Right => Vector2.right
            };

            attackCenter = attackOrigin.position;
            float weaponRange = Player.Equipment.EquippedWeapon.BaseWeaponRange;
            if (attackDir == Vector2.left || attackDir == Vector2.right)
            {
                attackRange = new Vector2(weaponRange, attackWidth);
                attackCenter += new Vector2(attackDir.x * (attackRange.x/2), 0f);
            }
            else
            {
                attackRange = new Vector2(attackWidth, weaponRange);
                attackCenter += attackDir == Vector2.up 
                    ? new Vector2(0f, attackRange.y / 2) 
                    : new Vector2(0f, -attackRange.y / 2);
            }

            attackDamage = Player.Equipment.EquippedWeapon.BaseWeaponDamage + (Player.Stats != null ? Player.Stats.TotalAttack : 0);
            attackCrit = Player.Equipment.EquippedWeapon.BaseWeaponCrit;
            
            Collider2D[] enemiesInRange = Physics2D.OverlapBoxAll(attackCenter, attackRange, 0f, enemyLayer);

            if (enemiesInRange.Length > 0)
                HitEnemy(enemiesInRange);
            
            Debug.Log("Primary Attack executed.");
        }
    }

    private void HitEnemy(Collider2D[] enemiesInRange)
    {
        float dmgAmount = attackDamage;
        if (isCounterAttacking)
            dmgAmount *= counterAttackMultiplier;
        
        dmgAmount *= (UnityEngine.Random.value <= attackCrit ? critDamageMultiplier : 1f);

        foreach (var col in enemiesInRange)
        {
            if (!col.CompareTag("EnemyHurtBox"))
                continue;
            
            if(col.transform.root.TryGetComponent(out EnemyHealth enemyHealth)) 
                enemyHealth.ApplyDamage((int)dmgAmount);
        }
    }

    public void EndAttack()
    {
        isAttacking = false;
        Debug.Log("attack finished");
    }

    #endregion

    #region Skill Logic

    private void HandleSkill()
    {
        if (skillBufferTimer > 0f && CanAct() && skillMeterAlwaysFull)
        {
            ExecuteSkill();
        }
        else if (skillBufferTimer > 0f && CanAct() && skillReady)
        {
            ExecuteSkill();
        }
    }

    public void ChargeSkillMeter(float amount)
    {
        SkillMeter = Mathf.Clamp(SkillMeter + amount, 0f, 1f);
        OnEnergyChanged?.Invoke(SkillMeter, 1f);
        Debug.Log("skill charge: " + SkillMeter);
        if (SkillMeter.Equals(1f))
            skillReady = true;
    }
    
    private void ExecuteSkill()
    {
        skillBufferTimer = 0f;
        
        if (skillPressed && skillTimer <= 0f && CanAct())
        {
            skillReady = false;
            CancelParry();
            PrimaryGemBehaviourDefinition specialDef = Player.Equipment.SpecialAttackDef;

            if (specialDef == null)
            {
                Debug.LogWarning("[PlayerCombatController] Cannot execute Special Attack: No Primary Gem equipped.");
                skillPressed = false;
                return;
            }
            
            isSkilling = true;
            CurrentSkillGemName = specialDef.GemName;
            skillCoroutine = StartCoroutine(PerformSkill(skillDamageTick, specialDef));

            OnEnergyChanged?.Invoke(0f, 1f); // I have no idea how it's keeping track of the skillmeter
        }
    }

    IEnumerator PerformSkill(float tick, PrimaryGemBehaviourDefinition def)
    {
        AttackContext context = Player.Equipment.GetModifiedAttackContext();
        float baseDamage = Player.Equipment.EquippedWeapon.BaseWeaponDamage + (Player.Stats != null ? Player.Stats.TotalAttack : 0);
        
        while(isSkilling)
        {
            def.Execute(context, baseDamage);
            yield return new WaitForSeconds(tick);
        }
    }

    public void EndSkill()
    {
        isSkilling = false;
        CurrentSkillGemName = string.Empty;
    }

    #endregion

    #region Input Handlers

    public void OnMove(InputValue value)
    {
        verticalInput = value.Get<Vector2>().y;
    }

    public void OnParry()
    {
        parryBufferTimer = parryBufferTime;
    }

    public void OnAttack()
    {
        attackPressed = true;
        attackBufferTimer = attackBufferTime;
    }

    public void OnSAttack(InputValue value)
    {
        if (value.isPressed)
        {
            skillPressed = true;
            skillReleased = false;
            skillBufferTimer = skillBufferTime;
        }
        else
        {
            skillPressed = false;
            skillReleased = true;
        }
    }

    #endregion
    
    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackCenter, attackRange);
        if (Player == null) return;
        if (Player.Equipment == null) return;
        if (Player.Equipment.SpecialAttackDef == null) return;
        Gizmos.DrawWireSphere(transform.position, Player.Equipment.SpecialAttackDef.SkillRange);
    }
}