using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum ParryDirection { Up, Down, Left, Right }
public enum AttackForce { Zero, Light, Medium, Heavy }

public class PlayerCombatController : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryActiveDuration = 0.2f;
    [SerializeField] private float parryRecoveryDuration = 0.3f;
    [SerializeField] private float parryBufferTime = 0.15f;
    [SerializeField] private float successfulParryVisualDuration = 0.15f;
    
    private float parryActiveTimer;
    private float parryRecoveryTimer;
    private float parryBufferTimer;
    private bool isParrying;
    private bool isParryInRecovery;
    private bool isParrySuccess;
    private ParryDirection parryDir;
    private Coroutine parrySuccessResetCoroutine;

    [Header("Attack Settings")]
    [SerializeField] private Transform attackOrigin;
    public LayerMask enemyLayer;
    [SerializeField] private float critDamageMultiplier = 1.33f;
    [SerializeField] private float counterAttackMultiplier = 1.2f;
    [SerializeField] private float attackWidth = 2f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackCoolDown = 0.2f;
    [SerializeField] private float counterAttackWindow = 0.5f;

    private float attackBufferTimer;
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
    [SerializeField] private float chargingSkillMinDur = 0.4f;
    [SerializeField] private float chargingSkillMaxDur = 1.5f;
    [SerializeField] private float fullChargeDamageMultiplier = 1.5f;
    [SerializeField] private float skillHoldThreshold = 0.12f;
    [SerializeField] private float skillReleaseBufferTime = 0.08f;
    [SerializeField] private float chargeSkillAmount = 0.2f;

    private float skillBufferTimer;
    private float skillTimer;
    private bool skillPressed;
    private bool isSkilling;
    private bool skillButtonHeld;
    private bool isChargingSkill;
    private float chargingSkillTimer;
    private bool skillFiredThisHold;
    private bool skillReady;
    private Coroutine skillCoroutine;

    private float verticalInput;
    private Player Player;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying || isParryInRecovery;
    public bool IsParrySuccess => isParrySuccess;
    public bool IsSkilling => isSkilling;
    public bool IsChargingSkill => isChargingSkill;
    public string CurrentSkillGemName { get; private set; }

    private float _skillMeter;
    public float SkillMeter 
    { 
        get => skillMeterAlwaysFull ? 1f : _skillMeter; 
        private set => _skillMeter = value; 
    }

    public event Action<float, float> OnEnergyChanged;

    private void Awake() => Player = GetComponentInParent<Player>();

    private void Start() 
    {
        if (skillMeterAlwaysFull) SkillMeter = 1f;
        OnEnergyChanged?.Invoke(SkillMeter, 1f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            OnEnergyChanged?.Invoke(SkillMeter, 1f);
        }
    }

    private void Update()
    {
        if (!Player.Controller.InputEnabled) return;

        HandleParry();
        HandleAttack();
        HandleSkill();
        UpdateTimers();

        if (skillButtonHeld)
        {
            chargingSkillTimer += Time.deltaTime;
            var specialDef = Player.Equipment?.SpecialAttackDef;

            if (specialDef != null && specialDef.SkillExecutionType == SkillExecutionType.Charged)
            {
                if (chargingSkillTimer >= skillHoldThreshold && !Player.Controller.IsChargingSkill)
                    Player.Controller.SetSkillCharging(true);
            }
        }
    }

    private void UpdateTimers()
    {
        if (parryBufferTimer > 0f) parryBufferTimer -= Time.deltaTime;
        if (attackBufferTimer > 0f) attackBufferTimer -= Time.deltaTime;
        if (skillBufferTimer > 0f) skillBufferTimer -= Time.deltaTime;
        
        attackTimer = isAttacking ? attackCoolDown : attackTimer - Time.deltaTime;
        skillTimer = isSkilling ? skillCoolDown : skillTimer - Time.deltaTime;

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

        if (isParryInRecovery)
        {
            parryRecoveryTimer -= Time.deltaTime;
            if (parryRecoveryTimer <= 0f)
            {
                isParryInRecovery = false;
                Player.Controller?.FreezeMovement(false);
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
            && !isAttacking
            && !isSkilling;
    }

    private bool CanReleaseSkill()
    {
        return Player.Controller.InputEnabled 
            && !Player.Controller.IsWallSliding 
            && !isParrying 
            && !isParryInRecovery 
            && !isAttacking
            && !isSkilling;
    }

    #region Parry Logic

    private void HandleParry()
    {
        if (parryBufferTimer > 0f && CanAct()) ExecuteParry();
    }

    private void ExecuteParry()
    {
        parryBufferTimer = 0f;
        isParrying = true;
        isParryInRecovery = false;
        parryActiveTimer = parryActiveDuration;
        Player.Controller?.FreezeMovement(true);
        parryDir = GetInputDirection();
    }

    private ParryDirection GetInputDirection()
    {
        if (verticalInput > 0.01f) return ParryDirection.Up;
        if (verticalInput < -0.01f && !Player.Controller.IsGrounded) return ParryDirection.Down;
        return Player.Controller.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;
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
        CancelParry();

        ChargeSkillMeter(chargeSkillAmount);
        isCounterAttacking = true;
        
        CancelInvoke(nameof(EndCounterAttackWindow));
        Invoke(nameof(EndCounterAttackWindow), counterAttackWindow);

        parrySuccessResetCoroutine = StartCoroutine(ExtendParryAnim(successfulParryVisualDuration));
    }

    private IEnumerator ExtendParryAnim(float delay)
    {
        isParrySuccess = true;
        yield return new WaitForSeconds(delay);
        isParrySuccess = false;
        parrySuccessResetCoroutine = null;
    }

    private void EndCounterAttackWindow() => isCounterAttacking = false;

    public void CancelParry()
    {
        if (parrySuccessResetCoroutine != null)
        {
            StopCoroutine(parrySuccessResetCoroutine);
            parrySuccessResetCoroutine = null;
        }

        isParrying = isParryInRecovery = isParrySuccess = false;
        parryActiveTimer = parryRecoveryTimer = 0f;
        Player.Controller?.FreezeMovement(false);
    }

    #endregion

    #region Attack Logic

    private void HandleAttack()
    {
        if (attackBufferTimer > 0f && CanAct()) ExecuteAttack();
    }

    private void ExecuteAttack()
    {
        attackBufferTimer = 0f;

        if (attackPressed && attackTimer <= 0f && CanAct())
        {
            CancelParry();
            attackPressed = false;

            if (Player.Equipment.EquippedWeapon == null) return;

            isAttacking = true;
            Vector2 attackDir = GetInputDirection() switch
            {
                ParryDirection.Up => Vector2.up,
                ParryDirection.Down => Vector2.down,
                ParryDirection.Left => Vector2.left,
                _ => Vector2.right
            };

            float weaponRange = Player.Equipment.EquippedWeapon.BaseWeaponRange;
            bool isHorizontal = attackDir.x != 0f;

            attackRange = isHorizontal ? new Vector2(weaponRange, attackWidth) : new Vector2(attackWidth, weaponRange);
            attackCenter = (Vector2)attackOrigin.position + (attackDir * (weaponRange * 0.5f));

            attackDamage = GetDamage();
            attackCrit = Player.Equipment.EquippedWeapon.BaseWeaponCrit;

            Collider2D[] enemiesInRange = Physics2D.OverlapBoxAll(attackCenter, attackRange, 0f, enemyLayer);
            if (enemiesInRange.Length > 0) HitEnemy(enemiesInRange);
        }
    }

    private float GetDamage() => Player.Stats != null ? Player.Stats.TotalAttack : 0f;

    private void HitEnemy(Collider2D[] enemiesInRange)
    {
        float dmg = attackDamage * (isCounterAttacking ? counterAttackMultiplier : 1f);
        dmg *= (UnityEngine.Random.value <= attackCrit ? critDamageMultiplier : 1f);

        foreach (var col in enemiesInRange)
        {
            if (col.CompareTag("EnemyHurtBox") && col.transform.root.TryGetComponent(out EnemyHealth enemyHealth))
            {
                enemyHealth.ApplyDamage((int)dmg);
            }
        }
    }

    public void EndAttack() => isAttacking = false;

    #endregion

    #region Skill Logic

    private void HandleSkill()
    {
        bool isReadyToFire = skillMeterAlwaysFull || SkillMeter > 0f;
        if (skillBufferTimer > 0f && CanReleaseSkill() && isReadyToFire)
        {
            ExecuteSkill();
        }
    }

    public void ChargeSkillMeter(float amount)
    {
        SkillMeter = Mathf.Clamp01(SkillMeter + amount);
        OnEnergyChanged?.Invoke(SkillMeter, 1f);
        if (Mathf.Approximately(SkillMeter, 1f)) skillReady = true;
    }

    private void ExecuteSkill()
    {
        skillBufferTimer = 0f;

        if (skillPressed && skillTimer <= 0f && CanReleaseSkill())
        {
            skillReady = false;
            CancelParry();
            var specialDef = Player.Equipment.SpecialAttackDef;

            if (specialDef == null)
            {
                skillPressed = false;
                Player.Controller.SetSkillCharging(false);
                return;
            }

            isSkilling = true;
            CurrentSkillGemName = specialDef.GemName;

            bool wasCharged = chargingSkillTimer >= chargingSkillMinDur;
            float chargeDamageMultiplier = 1f;

            if (wasCharged)
            {
                float chargeRatio = Mathf.InverseLerp(chargingSkillMinDur, chargingSkillMaxDur, chargingSkillTimer);
                chargeDamageMultiplier = Mathf.Lerp(1f, fullChargeDamageMultiplier, chargeRatio);
            }

            Debug.Log($"Timer: {chargingSkillTimer:F2} | WasCharged: {wasCharged} | Multiplier: {chargeDamageMultiplier:F2} | Final Dmg: {GetDamage() * chargeDamageMultiplier}");

            Player.Controller.SetSkillCharging(false);
            if (wasCharged) Player.Controller.SetSkillGravityZero(true);

            if (specialDef.SkillExecutionType == SkillExecutionType.Held)
            {
                skillCoroutine = StartCoroutine(PerformTimedSkill(specialDef));
            }
            else
            {
                if (specialDef.SkillType == SkillType.Single)
                {
                    SkillMeter = 0f;
                    OnEnergyChanged?.Invoke(SkillMeter, 1f);
                    PerformSingleSkill(specialDef, chargeDamageMultiplier);
                }
                else if (specialDef.SkillType == SkillType.Timed)
                {
                    skillCoroutine = StartCoroutine(PerformTimedSkill(specialDef, chargeDamageMultiplier));
                }
            }
        }
    }

    private void PerformSingleSkill(PrimaryGemBehaviourDefinition def, float multiplier)
    {
        def.Execute(Player.Equipment.GetModifiedAttackContext(), GetDamage() * multiplier);
    }

    private IEnumerator PerformTimedSkill(PrimaryGemBehaviourDefinition def, float fixedChargeMultiplier = 1f)
    {
        var context = Player.Equipment.GetModifiedAttackContext();
        float tick = def.EnergyDrainTick > 0f ? def.EnergyDrainTick : 0.16f;

        bool isHeld = def.SkillExecutionType == SkillExecutionType.Held;
        float totalTicks = chargingSkillMaxDur / tick;
        float energyCostPerTick = 1f / totalTicks;

        if (!isHeld && !skillMeterAlwaysFull)
        {
            SkillMeter = 0f;
            OnEnergyChanged?.Invoke(SkillMeter, 1f);
        }

        while (isSkilling && (skillMeterAlwaysFull || (isHeld ? SkillMeter > 0f : true)))
        {
            float dynamicRampMultiplier = fixedChargeMultiplier;

            if (isHeld && !skillMeterAlwaysFull)
            {
                SkillMeter = Mathf.Clamp01(SkillMeter - energyCostPerTick);
                OnEnergyChanged?.Invoke(SkillMeter, 1f);

                float chargeRatio = Mathf.InverseLerp(chargingSkillMinDur, chargingSkillMaxDur, chargingSkillTimer);
                dynamicRampMultiplier = Mathf.Lerp(1f, fullChargeDamageMultiplier, chargeRatio);
            }

            float currentTickDamage = GetDamage() * dynamicRampMultiplier;
            def.Execute(context, currentTickDamage);

            yield return new WaitForSeconds(tick);

            if (isHeld) chargingSkillTimer += tick;
        }

        EndSkill();
    }

    public void EndSkill()
    {
        if (skillCoroutine != null)
        {
            StopCoroutine(skillCoroutine);
            skillCoroutine = null;
        }

        isSkilling = isChargingSkill = skillButtonHeld = false;
        CurrentSkillGemName = string.Empty;
        Player.Controller.SetSkillGravityZero(false);
    }

    public void CancelSkillStates()
    {
        isChargingSkill = false;
        isSkilling = false;
        skillButtonHeld = false;
        skillFiredThisHold = true;

        var playerController = Player.Controller;
        if (playerController != null)
        {
            playerController.SetSkillCharging(false);
            playerController.SetSkillGravityZero(false);
        }
    }

    #endregion

    #region Input Handlers

    public void OnMove(InputValue value) => verticalInput = value.Get<Vector2>().y;
    public void OnParry() => parryBufferTimer = parryBufferTime;
    public void OnAttack() { attackPressed = true; attackBufferTimer = parryBufferTime; }

    public void OnSAttack(InputValue value)
    {
        if (Player.Controller.IsWallSliding) return;

        var specialDef = Player.Equipment?.SpecialAttackDef;

        if (value.isPressed)
        {
            if (!skillMeterAlwaysFull && SkillMeter <= 0f) return;

            skillButtonHeld = true;
            chargingSkillTimer = 0f;
            skillFiredThisHold = false;

            if (specialDef != null && specialDef.SkillExecutionType == SkillExecutionType.Held)
            {
                TriggerSkillRelease();
                return;
            }

            isChargingSkill = true;
            CancelInvoke(nameof(AutoFireAtMaxCharge));
            Invoke(nameof(AutoFireAtMaxCharge), chargingSkillMaxDur);
        }
        else
        {
            skillButtonHeld = false;

            if (specialDef != null && specialDef.SkillExecutionType == SkillExecutionType.Held)
            {
                if (isSkilling)
                {
                    float remaining = specialDef.MinimumHeldDuration - chargingSkillTimer;
                    if (remaining > 0f) StartCoroutine(DelayedEndSkill(remaining));
                    else EndSkill();
                }
                return;
            }

            isChargingSkill = false;
            StopChargingPhysics();

            if (!skillFiredThisHold) TriggerSkillRelease();
        }
    }

    private void TriggerSkillRelease()
    {
        skillPressed = true;
        skillBufferTimer = skillReleaseBufferTime;
    }

    private IEnumerator DelayedEndSkill(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!skillButtonHeld && isSkilling) EndSkill();
    }

    private void AutoFireAtMaxCharge()
    {
        if (!skillButtonHeld) return;

        chargingSkillTimer = chargingSkillMaxDur;
        skillButtonHeld = isChargingSkill = false;
        skillFiredThisHold = true;

        TriggerSkillRelease();
        StopChargingPhysics();
    }

    private void StopChargingPhysics()
    {
        Player.Controller.SetSkillCharging(false);
        CancelInvoke(nameof(AutoFireAtMaxCharge));
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackCenter, attackRange);
    }
}