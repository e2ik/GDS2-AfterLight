using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Combo Settings")]
    [SerializeField] private int maxComboCount = 3;
    [SerializeField] private float comboResetDelay = 1.0f;

    private int currentComboIndex = 0;
    private bool canBufferNextCombo = false;
    private bool comboQueued = false;
    private float comboResetTimer;
    private float[] comboDamageMultipliers = new float[] { 1.0f, 1.25f, 1.5f };
    public int CurrentComboIndex => currentComboIndex;

    [Header("Dash Conflict Settings")]
    [SerializeField] private float dashAttackConflictWindow = 0.2f;
    private float dashAttackBlockTimer;

    private float attackBufferTimer;
    private float attackDurationTimer;
    private Vector2 attackRange;
    private Vector2 attackCenter;
    private float attackDamage;
    private float attackCrit;
    private float attackTimer;
    private bool attackPressed;
    private bool isAttacking;
    private bool isCounterAttacking;

    // hit enemies
    private HashSet<EnemyHealth> enemiesHitThisAttack = new HashSet<EnemyHealth>();

    [Header("Skill Settings")]
    [SerializeField] private bool skillMeterAlwaysFull;
    [SerializeField] private float skillCoolDown = 0.2f;
    [SerializeField] private float chargingSkillMinDur = 0.4f;
    [SerializeField] private float chargingSkillMaxDur = 1.5f; // must be 1 or more
    [SerializeField] private float fullChargeDamageMultiplier = 1.5f;
    [SerializeField] private float skillHoldThreshold = 0.12f;
    [SerializeField] private float skillReleaseBufferTime = 0.08f;
    [SerializeField] private float chargeSkillAmount = 0.2f;
    [Range(0.1f, 1f)] public float SkillActivationCost { get; private set; }

    private PrimaryGemBehaviourDefinition currentSkillDef;
    private float skillBufferTimer;
    private float skillTimer;
    private bool skillPressed;
    private bool isSkilling;
    private bool skillButtonHeld;
    private bool isChargingSkill;
    private float chargingSkillTimer;
    private bool skillFiredThisHold;
    private Coroutine skillCoroutine;
    private float singleSkillCostTick;
    private float singleSkillChargeCost;

    private float verticalInput;
    private Player Player;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying || isParryInRecovery;
    public bool IsParrySuccess => isParrySuccess;
    public bool IsSkilling => isSkilling;
    public bool IsChargingSkill => isChargingSkill;
    public string CurrentSkillGemName { get; private set; }
    public float ChargingSkillTimer => chargingSkillTimer;
    public float ChargingSkillMaxDur => chargingSkillMaxDur;

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

        currentSkillDef = Player.Equipment?.SpecialAttackDef;

        HandleParry();
        HandleAttack();
        HandleSkill();
        UpdateTimers();

        if (isAttacking && Player.Equipment?.EquippedWeapon != null)
        {
            PerformAttackHitboxCheck();
        }

        if (!skillButtonHeld) return;

        chargingSkillTimer += Time.deltaTime;

        if (currentSkillDef != null && currentSkillDef.SkillExecutionType == SkillExecutionType.Charged)
        {
            if (chargingSkillTimer >= skillHoldThreshold)
            {
                if (!Player.Controller.IsChargingSkill)
                {
                    Player.Controller.SetSkillCharging(true);
                }

                SingleSkillChargeCost();
            }
        }
    }

    private void SingleSkillChargeCost()
    {
        if (skillMeterAlwaysFull || !isChargingSkill) return;

        float maxCost = currentSkillDef.SkillCost;
        float availableCost = SkillMeter - maxCost;

        if (availableCost <= 0f || singleSkillChargeCost >= maxCost)
        {
            AutoFireAtMaxCharge(true, chargingSkillTimer);
            return;
        }

        float amount = singleSkillCostTick * Time.deltaTime;
        amount = Mathf.Min(amount, maxCost - singleSkillChargeCost);

        SkillMeter -= amount;
        singleSkillChargeCost += amount;
        OnEnergyChanged?.Invoke(SkillMeter, 1f);

        if (SkillMeter <= maxCost)
        {
            SkillMeter = currentSkillDef.SkillCost;
            OnEnergyChanged?.Invoke(SkillMeter, 1f);
            AutoFireAtMaxCharge(true, chargingSkillTimer);
        }
    }

    private void UpdateTimers()
    {
        if (parryBufferTimer > 0f) parryBufferTimer -= Time.deltaTime;
        if (attackBufferTimer > 0f) attackBufferTimer -= Time.deltaTime;
        if (skillBufferTimer > 0f) skillBufferTimer -= Time.deltaTime;
        if (dashAttackBlockTimer > 0f) dashAttackBlockTimer -= Time.deltaTime;

        attackTimer = isAttacking ? attackCoolDown : attackTimer - Time.deltaTime;
        skillTimer = isSkilling ? skillCoolDown : skillTimer - Time.deltaTime;

        if (isAttacking)
        {
            attackDurationTimer -= Time.deltaTime;
            if (attackDurationTimer <= 0f)
            {
                EndAttack(); // Failsafe triggered if animation event is skipped/missed
            }
        }

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
            && !isChargingSkill
            && !Player.Controller.IsNeutralDash
            && !isParryInRecovery
            && !isSkilling;
    }

    private bool CanBufferAttack()
    {
        return Player.Controller.InputEnabled
            && !Player.Controller.IsWallSliding
            && !Player.Controller.IsChargingSkill
            && !isChargingSkill
            && !Player.Controller.IsNeutralDash
            && !isParryInRecovery
            && !isSkilling;
    }

    private bool CanReleaseSkill()
    {
        return Player.Controller.InputEnabled
               && !Player.Controller.IsWallSliding
               && !isParrying
               && !isParryInRecovery
               && !isSkilling;
    }

    #region Parry Logic

    private void HandleParry()
    {
        if (parryBufferTimer > 0f && CanAct()) ExecuteParry();
    }

    private void ExecuteParry()
    {
        ForceCancelAttack();
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

    #region Attack & Combo Logic

    private void HandleAttack()
    {
        if (dashAttackBlockTimer > 0f) return;

        if (attackBufferTimer > 0f && isAttacking && canBufferNextCombo && CanBufferAttack())
        {
            comboQueued = true;
            attackBufferTimer = 0f;
        }

        if (attackBufferTimer > 0f && CanAct()) ExecuteAttack();
        if (comboQueued && !isAttacking && attackTimer <= 0f && CanBufferAttack()) ExecuteAttack();

        if (currentComboIndex > 0 && !isAttacking)
        {
            comboResetTimer += Time.deltaTime;
            if (comboResetTimer >= comboResetDelay) ResetCombo();
        }
    }

    private void ExecuteAttack()
    {
        attackBufferTimer = 0f;

        if ((canBufferNextCombo && comboQueued) || (!isAttacking && attackTimer <= 0f && CanAct()))
        {
            CancelParry();
            attackPressed = false;
            comboQueued = false;
            canBufferNextCombo = false;

            if (Player.Equipment.EquippedWeapon == null) return;

            currentComboIndex = (currentComboIndex % maxComboCount) + 1;

            isAttacking = true;
            attackDurationTimer = attackDuration;

            enemiesHitThisAttack.Clear();

            if (Player.Controller != null && Player.Controller.IsDashing)
            {
                Player.Controller.SetDashLockedDuringAttack(true);
            }
        }
    }

    private void PerformAttackHitboxCheck()
    {
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

    private float GetDamage()
    {
        float baseDmg = Player.Stats != null ? Player.Stats.TotalAttack : 0f;

        int multiplierIndex = Mathf.Clamp(currentComboIndex - 1, 0, comboDamageMultipliers.Length - 1);
        float comboMultiplier = comboDamageMultipliers[multiplierIndex];

        return baseDmg * comboMultiplier;
    }

    private void HitEnemy(Collider2D[] enemiesInRange)
    {
        float dmg = attackDamage * (isCounterAttacking ? counterAttackMultiplier : 1f);
        dmg *= (UnityEngine.Random.value <= attackCrit ? critDamageMultiplier : 1f);

        foreach (var col in enemiesInRange)
        {
            if (col.CompareTag("EnemyHurtBox") && col.transform.root.TryGetComponent(out EnemyHealth enemyHealth))
            {
                // Only damage each enemy once per attack swing
                if (!enemiesHitThisAttack.Contains(enemyHealth))
                {
                    enemiesHitThisAttack.Add(enemyHealth);
                    enemyHealth.ApplyDamage((int)dmg);
                }
            }
        }
    }

    public void EndAttack()
    {
        isAttacking = false;
        attackDurationTimer = 0f;
        canBufferNextCombo = false;

        if (!comboQueued)
        {
            ResetCombo();
        }

        if (Player.Controller != null)
        {
            Player.Controller.SetDashLockedDuringAttack(false);
        }
    }

    private void ResetCombo()
    {
        currentComboIndex = 0;
        comboQueued = false;
        canBufferNextCombo = false;
    }

    #endregion

    #region Skill Logic

    private void HandleSkill()
    {
        SkillActivationCost = currentSkillDef != null ? currentSkillDef.SkillCost : 0f;
        bool isReadyToFire = skillMeterAlwaysFull || SkillMeter >= SkillActivationCost;
        if (skillBufferTimer > 0f && CanReleaseSkill() && isReadyToFire)
        {
            ExecuteSkill();
        }
    }

    public void ChargeSkillMeter(float amount)
    {
        SkillMeter = Mathf.Clamp01(SkillMeter + amount);
        OnEnergyChanged?.Invoke(SkillMeter, 1f);
    }

    private void ExecuteSkill()
    {
        skillBufferTimer = 0f;

        if (!skillPressed || !(skillTimer <= 0f) || !CanReleaseSkill()) return;

        skillPressed = false;
        CancelParry();
        ForceCancelAttack(); // a skill firing mid-swing interrupts the attack

        var specialDef = Player.Equipment.SpecialAttackDef;

        if (specialDef == null)
        {
            Player.Controller.SetSkillCharging(false);
            return;
        }

        isSkilling = true;
        CurrentSkillGemName = specialDef.GemName;

        bool wasCharged = chargingSkillTimer >= chargingSkillMinDur;
        float chargeDamageMultiplier = 1f;
        float chargeRatio = 0f;

        if (wasCharged)
        {
            chargeRatio = Mathf.InverseLerp(chargingSkillMinDur, chargingSkillMaxDur, chargingSkillTimer);
            chargeDamageMultiplier = Mathf.Lerp(1f, fullChargeDamageMultiplier, chargeRatio);
        }

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
                PerformSingleSkill(specialDef, chargeRatio, chargeDamageMultiplier);
                EndSkill();
            }
            else if (specialDef.SkillType == SkillType.Timed)
            {
                skillCoroutine = StartCoroutine(PerformTimedSkill(specialDef, chargeDamageMultiplier, chargeRatio));
            }
        }
    }

    private void PerformSingleSkill(PrimaryGemBehaviourDefinition def, float chargePercentage, float multiplier)
    {
        SkillMeter -= SkillActivationCost;
        OnEnergyChanged?.Invoke(SkillMeter, 1f);
        def.Execute(Player.Equipment.GetModifiedAttackContext(), GetDamage() * multiplier, chargePercentage);
    }

    private IEnumerator PerformTimedSkill(PrimaryGemBehaviourDefinition def, float fixedChargeMultiplier = 1f, float chargePercentage = 0f)
    {
        var context = Player.Equipment.GetModifiedAttackContext();
        float tick = def.EnergyDrainTick > 0f ? def.EnergyDrainTick : 0.16f;

        bool isHeld = def.SkillExecutionType == SkillExecutionType.Held;
        float totalTicks = chargingSkillMaxDur / tick;
        float energyCostPerTick = 1f / totalTicks;

        if (!isHeld && !skillMeterAlwaysFull)
        {
            SkillMeter -= SkillActivationCost;
            OnEnergyChanged?.Invoke(SkillMeter, 1f);
        }

        while (isSkilling && (skillMeterAlwaysFull || (isHeld ? SkillMeter > 0f : true)))
        {
            float dynamicRampMultiplier = fixedChargeMultiplier;
            float currentChargePercentage = chargePercentage;

            if (isHeld && !skillMeterAlwaysFull)
            {
                SkillMeter = Mathf.Clamp01(SkillMeter - energyCostPerTick);
                OnEnergyChanged?.Invoke(SkillMeter, 1f);

                float chargeRatio = Mathf.InverseLerp(chargingSkillMinDur, chargingSkillMaxDur, chargingSkillTimer);
                dynamicRampMultiplier = Mathf.Lerp(1f, fullChargeDamageMultiplier, chargeRatio);
                currentChargePercentage = chargeRatio;
            }

            float currentTickDamage = GetDamage() * dynamicRampMultiplier;
            def.Execute(context, currentTickDamage, currentChargePercentage);

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

    #region Input Handlers & Animator Hooks

    public void OnMove(InputValue value) => verticalInput = value.Get<Vector2>().y;
    public void OnParry() => parryBufferTimer = parryBufferTime;

    public void OnAttack()
    {
        if (IsParrying || isChargingSkill) return;

        attackPressed = true;
        attackBufferTimer = parryBufferTime;

        if (isAttacking && canBufferNextCombo)
        {
            comboQueued = true;
        }
    }

    public void NotifyDashInputReceived()
    {
        dashAttackBlockTimer = dashAttackConflictWindow;
    }

    public void OnSAttack(InputValue value)
    {
        if (Player.Controller.IsWallSliding) return;

        var specialDef = Player.Equipment?.SpecialAttackDef;

        if (value.isPressed)
        {
            if (specialDef == null) return;
            if (!skillMeterAlwaysFull && SkillMeter <= 0f) return;

            skillButtonHeld = true;
            chargingSkillTimer = 0f;
            singleSkillChargeCost = 0f;
            skillFiredThisHold = false;

            if (specialDef.SkillExecutionType == SkillExecutionType.Held)
            {
                TriggerSkillRelease();
                return;
            }

            isChargingSkill = true;
            singleSkillCostTick = specialDef.SkillCost / chargingSkillMaxDur;
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

    private void AutoFireAtMaxCharge(bool triggeredEarly = false, float chargingDur = 0f)
    {
        if (!skillButtonHeld) return;

        chargingSkillTimer = triggeredEarly ? chargingDur : chargingSkillMaxDur;
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

    public void ForceCancelAttack()
    {
        isAttacking = false;
        attackDurationTimer = 0f;
        enemiesHitThisAttack.Clear();
        ResetCombo();

        attackBufferTimer = 0f;

        if (Player.Controller != null)
        {
            Player.Controller.SetDashLockedDuringAttack(false);
        }
    }

    public void OpenComboWindow()
    {
        canBufferNextCombo = true;
        comboResetTimer = 0f;
    }

    public void CloseComboWindow()
    {
        canBufferNextCombo = false;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackCenter, attackRange);
    }
}