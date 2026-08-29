using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

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
    private PlayerStats pStats;
    private PlayerController pController;
    private PlayerEquipmentManager equipmentManager;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying || isParryInRecovery; // just for now we can make visual distinction later
    

    private void Awake()
    {
        pStats = GetComponent<PlayerStats>();
        pController = GetComponent<PlayerController>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();
    }

    private void Update()
    {
        if (!pController.InputEnabled) return;

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
                if (pController != null)
                    pController.FreezeMovement(false);
            }
        }
    }

    private bool CanAct()
    {
        return pController.InputEnabled 
            && !pController.IsWallSliding 
            && !pController.IsChargingSkill 
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
        
        if (pController != null) 
            pController.FreezeMovement(true);

        parryDir = GetInputDirection();

        Debug.Log($"Parry Executed! Direction: {parryDir}");
    }

    private ParryDirection GetInputDirection()
    {
        ParryDirection inputDir = pController.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;
        if (verticalInput > 0.01f) 
            inputDir = ParryDirection.Up;
        else if (verticalInput < -0.01f && !pController.IsGrounded) 
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
        // Cancel timers and restore movement instantly on successful parry
        isParrying = false;
        isParryInRecovery = false;
        parryActiveTimer = 0f;
        parryRecoveryTimer = 0f;
        
        ChargeSkillMeter(chargeSkillAmount);

        if (pController != null)
        {
            pController.FreezeMovement(false);
        }

        //slow down surroundings for effect when successful parry/counter attacking?
        //Time.timeScale = 0.7f;
        isCounterAttacking = true;
        Invoke(nameof(EndCounterAttackWindow), counterAttackWindow);

        Debug.Log("PARRY SUCCESSFUL!");
    }

    private void EndCounterAttackWindow()
    {
        isCounterAttacking = false;
        //Time.timeScale = 1f;
    }

    public void CancelParry()
    {
        if (!isParrying && !isParryInRecovery) return;

        isParrying = false;
        isParryInRecovery = false;
        parryActiveTimer = 0f;
        parryRecoveryTimer = 0f;

        // Reset movement freeze and gravity state
        if (pController != null)
        {
            pController.FreezeMovement(false);
            // pController.SetParryGravity(false);
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
            attackPressed = false;
            
            if (equipmentManager.EquippedWeapon == null)
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
            float weaponRange = equipmentManager.EquippedWeapon.BaseWeaponRange;
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

            attackDamage = equipmentManager.EquippedWeapon.BaseWeaponDamage + (pStats != null ? pStats.TotalAttack : 0);
            attackCrit = equipmentManager.EquippedWeapon.BaseWeaponCrit;
            
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
        Debug.Log("skill charge: " + SkillMeter);
        if (SkillMeter.Equals(1f))
            skillReady = true;
    }
    
    private void ExecuteSkill()
    {
        skillBufferTimer = 0f;
        skillReady = false;
        
        if (skillPressed && skillTimer <= 0f && CanAct())
        {
            PrimaryGemBehaviourDefinition specialDef = equipmentManager.SpecialAttackDef;

            if (specialDef == null)
            {
                Debug.LogWarning("[PlayerCombatController] Cannot execute Special Attack: No Primary Gem equipped.");
                skillPressed = false;
                return;
            }

            AttackContext context = equipmentManager.GetModifiedAttackContext();
            float baseDamage = equipmentManager.EquippedWeapon.BaseWeaponDamage + (pStats != null ? pStats.TotalAttack : 0);
            specialDef.Execute(context, baseDamage);
            
            Invoke(nameof(EndSkill), 0.2f); // change to animation trigger
        }
    }

    public void EndSkill()
    {
        isSkilling = false;
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
    }
}