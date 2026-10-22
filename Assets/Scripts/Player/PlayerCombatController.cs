using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using FMOD.Studio;

public enum ParryDirection { Up, Down, Left, Right }
public enum AttackForce { Zero, Light, Medium, Heavy }

[RequireComponent(typeof(Player))]
public class PlayerCombatController : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryActiveDuration = 0.2f;
    [SerializeField] private float parryRecoveryDuration = 0.3f;
    [SerializeField] private float parryBufferTime = 0.15f;
    [SerializeField] private float successfulParryVisualDuration = 0.15f;
    [SerializeField] private float parryWindowExtension = 0.1f;

    private float parryActiveTimer;
    private float parryWindowExtensionTimer;
    private float parryRecoveryTimer;
    private float parryBufferTimer;
    private bool isParrying;
    private bool isParryInRecovery;
    private bool isParrySuccess;
    private ParryDirection parryDir;
    private Coroutine parrySuccessResetCoroutine;

    [Header("Attack Settings")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private float damageScalingExponent = 0.8f;
    public LayerMask enemyLayer;
    [SerializeField] private float critDamageMultiplier = 1.33f;
    [SerializeField] private float counterAttackMultiplier = 1.2f;
    [SerializeField] private float plungeDmgMaxMultiplier = float.MaxValue;
    [SerializeField] private float plungeAdjustedDmg = 1f;
    [SerializeField] private float plungeGraceWindow = 0.15f;
    [SerializeField] private float plungeBounceForce = 10f;
    [SerializeField, Range(0f, 1f)] private float plungeBounceSideAmount = 0.25f;
    [SerializeField] private float plungeRecoveryDuration = 0.3f;
    [SerializeField, Min(0f)] private float minPlungeAirTime = 0.15f;
    private float airTime;
    private float attackStartAirTime;
    [SerializeField] private float attackWidth = 2f;

    [Header("Attack Reach")]
    [SerializeField] private bool clampAttackToBodyBottom = true;
    [SerializeField] private float attackBottomPadding = 0f;
    [SerializeField] private bool clampAttackToBodyTop = true;
    [SerializeField] private float attackTopPadding = 0f;
    [SerializeField] private bool debugAttackReach = false;
    [SerializeField, Min(0f)] private float debugLineDuration = 0.5f;
    [SerializeField, Min(0f)] private float debugPreviewRange = 2f;
    [SerializeField] private bool blockAttacksThroughWalls = true;
    [SerializeField] private LayerMask attackBlockMask;
    private readonly RaycastHit2D[] attackBlockHits = new RaycastHit2D[8];
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackCoolDown = 0.2f;
    [SerializeField] private float attackBufferTime = 0.15f;
    [SerializeField] private float counterAttackWindow = 0.5f;
    [SerializeField] private float passiveEnergyCooldown = 4f;
    [SerializeField] private float passiveEnergyRegenMult = 0.2f; // 1f = 1 sec for full bar, .25f = 4 secs etc.
    [SerializeField, Range(0f, 1f)] private float passiveEnergyCap = 0.5f;
    private float passiveEnergyRecoveryTimer;

    [Header("Combo Settings")]
    [SerializeField] private int maxComboCount = 3;
    [SerializeField, Min(0f)] private float comboGraceTime = 0.25f;
    [SerializeField] private bool useComboCloseEvent = false;
    private float comboGraceTimer;

    private int currentComboIndex = 0;
    private bool canBufferNextCombo = false;
    private bool comboQueued = false;
    [SerializeField] private float[] comboDamageMultipliers = { 1.0f, 1.25f, 1.5f };
    [SerializeField] private float dashAttackMultiplier = 1.25f;
    [SerializeField] private float airAttackMultiplier = 1.1f;
    [SerializeField] private bool airAttackAllAround = true;
    [SerializeField, Min(0f)] private float airAttackRadiusMultiplier = 1f;
    [SerializeField] private Vector2 airAttackCenterOffset = Vector2.zero;
    private bool lastAttackWasCircle;
    private float lastAttackRadius;
    [SerializeField] private bool endAirAttackOnLanding = true;
    [SerializeField, Range(0f, 1f)] private float attackActiveStart = 0.3f;
    [SerializeField, Range(0f, 1f)] private float attackActiveEnd = 1f;
    private float attackTotalDuration;
    public int CurrentComboIndex => currentComboIndex;
    public int AttackSerial { get; private set; }
    public bool IsDashAttack => isAttacking && isDashAttack;

    [Header("Input Conflict Settings")]
    [SerializeField] private float dashAttackConflictWindow = 0.2f;
    private float dashAttackBlockTimer;
    [SerializeField] private float jumpAttackConflictWindow = 0.2f;
    private float jumpAttackBlockTimer;

    private float attackBufferTimer;
    private float attackDurationTimer;
    private Vector2 attackRange;
    private Vector2 attackCenter;
    private float attackTimer;
    private bool isAttacking;
    private bool attackStartedGrounded;
    private bool isCounterAttacking;
    private bool isDashAttack;
    private bool isPlunging;
    private float plungeGraceTimer;
    private float plungeRecoveryTimer;
    private bool passiveTriggeredThisAttack;

    private HashSet<EnemyHealth> enemiesHitThisAttack = new HashSet<EnemyHealth>();

    [Header("Skill Settings")]
    private static bool skillMeterAlwaysFull => GameManager.Instance != null && GameManager.Instance.SkillMeterAlwaysFull;
    [SerializeField] private float skillCoolDown = 0.2f;
    [SerializeField] private float chargingSkillMinDur = 0.4f;
    [SerializeField] private float chargingSkillMaxDur = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float heldSkillFastestTickMultiplier = 0.7f;
    [SerializeField, Min(0.01f)] private float heldSkillRampDuration = 0.6f;
    private float heldRampStartTime = -1f;
    [SerializeField] private float fullChargeDamageMultiplier = 1.5f;
    [SerializeField] private float skillHoldThreshold = 0.12f;
    [SerializeField] private float skillReleaseBufferTime = 0.08f;
    [SerializeField] private float chargeSkillAmount = 0.2f;
    [SerializeField] private float skillGemModifierBonus = 1f;
    [SerializeField] private float attackGemModifierBonus = 0.25f;
    [SerializeField] private bool attackGemHyperArmor = true;

    [System.Serializable]
    public class SkillHyperArmorCombo
    {
        public PrimaryGemBehaviourDefinition skillGem;
        public SecondaryGemBehaviourDefinition secondaryGem;
    }

    [Header("Gear Combos")]
    [SerializeField] private SkillHyperArmorCombo[] skillHyperArmorCombos;
    [SerializeField, Range(0f, 1f)] private float attackGemCooldownReduction = 0.3f;
    [SerializeField] private float attackGemRangeBonus = 1f;
    [SerializeField, Range(0f, 1f)] private float attackGemAttackSpeedBonus = 0.15f;
    [SerializeField, Min(0f)] private float attackGemDashMultiplierBonus = 0.25f;
    [SerializeField, Min(0f)] private float attackGemPlungeMultiplierBonus = 0.25f;

    [Header("Reflect")]
    [SerializeField, Min(0f)] private float reflectBonusPerDefense = 0.02f;
    [SerializeField, Min(1f)] private float maxDefenseReflectMultiplier = 3f;
    [SerializeField] private bool reflectGemParriesHeavy = true;

    [Header("FMOD Events")]
    [SerializeField] private EventReference parryEvent;
    [SerializeField] private EventReference chargingSkillEvent;

    private EventInstance chargingEventInstance;

    public float SkillActivationCost { get; private set; }
    private const float DefaultEnergyDrainTick = 0.16f;

    private PrimaryGemBehaviourDefinition currentSkillDef;
    private float skillBufferTimer;
    private float skillTimer;
    private bool skillPressed;
    private bool isSkilling;
    private bool skillButtonHeld;
    private bool isChargingSkill;
    private float chargingSkillTimer;
    private bool skillFiredThisHold;
    private SkillExecutionType activeSkillExecutionType;
    private Coroutine skillCoroutine;
    private float singleSkillCostTick;
    private float singleSkillChargeCost;
    private Coroutine plungeCoroutine;

    private Player player;
    private PlayerController movement;

    public bool IsAttacking => isAttacking;
    public bool AttackStartedGrounded => attackStartedGrounded;
    public bool IsPlunging => isPlunging;
    public int PlungeHitCount { get; private set; }
    public bool WasRecentlyPlunging => plungeGraceTimer > 0f;
    public bool IsParrying => isParrying || isParryInRecovery;
    public bool IsParrySuccess => isParrySuccess;
    public bool IsSkilling => isSkilling;
    public bool IsSkillingWithMovementLock => isSkilling && activeSkillExecutionType != SkillExecutionType.Held;
    public bool IsHeldSkillActive => isSkilling && activeSkillExecutionType == SkillExecutionType.Held;
    public float HeldSkillRamp => heldRampStartTime < 0f ? 0f : Mathf.Clamp01((Time.time - heldRampStartTime) / heldSkillRampDuration);
    public float HeldSkillTickMultiplier => Mathf.Lerp(1f, heldSkillFastestTickMultiplier, HeldSkillRamp);
    public bool IsChargeInputHeld => isChargingSkill;
    public string CurrentSkillGemName { get; private set; }
    public float ChargingSkillTimer => chargingSkillTimer;
    public float ChargingSkillMaxDur => chargingSkillMaxDur;
    public bool IsSkillReady =>
        currentSkillDef != null
        && skillTimer <= 0f
        && (skillMeterAlwaysFull || SkillMeter >= SkillActivationCost);

    public bool HasSkillEquipped => player != null && player.Equipment != null && player.Equipment.SpecialAttackDef != null;

    public bool HasEnoughSkillEnergy
    {
        get
        {
            if (!HasSkillEquipped) return false;
            return skillMeterAlwaysFull || SkillMeter >= player.Equipment.SpecialAttackDef.SkillCost;
        }
    }

    private float _skillMeter;
    public float SkillMeter
    {
        get => skillMeterAlwaysFull ? 1f : _skillMeter;
        private set => _skillMeter = value;
    }

    public event Action<float, float> OnEnergyChanged;
    public event Action OnParrySuccess;
    public event Action<float> OnGemEnergyGained;
    public static event Action<PlayerCombatController, float> AnyGemEnergyGained;

    private void RaiseEnergyChanged() => OnEnergyChanged?.Invoke(SkillMeter, 1f);

    private void Awake()
    {
        player = GetComponent<Player>();
        movement = player.Controller;
    }

    private void Start()
    {
        if (skillMeterAlwaysFull) SkillMeter = 1f;
        RaiseEnergyChanged();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RaiseEnergyChanged();
        }

        if (comboDamageMultipliers.Length < maxComboCount)
        {
            Debug.LogWarning(
                $"{name}: comboDamageMultipliers has {comboDamageMultipliers.Length} entries but " +
                $"maxComboCount is {maxComboCount} - combo hits beyond entry {comboDamageMultipliers.Length} " +
                "will silently reuse the last multiplier.", this);
        }
    }

    private void Update()
    {
        if (!movement.InputEnabled) return;

        currentSkillDef = player.Equipment?.SpecialAttackDef;
        airTime = movement.IsGrounded ? 0f : airTime + Time.deltaTime;

        HandleParry();
        HandleAttack();
        HandleSkill();
        UpdateTimers();

        if (endAirAttackOnLanding && isAttacking && !attackStartedGrounded && !isDashAttack && movement.IsGrounded)
        {
            EndAttack();
        }

        if (isAttacking && player.Equipment?.EquippedWeapon != null)
        {
            PerformAttackHitboxCheck();
        }

        if (!skillButtonHeld) return;

        if (movement.IsWallSliding)
        {
            RefundChargeCost();
            skillButtonHeld = false;
            isChargingSkill = false;
            skillFiredThisHold = true;
            StopChargingPhysics();
            return;
        }

        if (jumpAttackBlockTimer > 0f) return;

        chargingSkillTimer += Time.deltaTime;

        if (currentSkillDef != null && currentSkillDef.SkillExecutionType == SkillExecutionType.Charged)
        {
            if (chargingSkillTimer >= skillHoldThreshold)
            {
                if (!movement.IsChargingSkill)
                {
                    movement.SetSkillCharging(true);
                }

                SingleSkillChargeCost();
            }
        }
    }

    private void SingleSkillChargeCost()
    {
        if (skillMeterAlwaysFull || !isChargingSkill) return;

        float remaining = currentSkillDef.SkillCost - singleSkillChargeCost;
        if (remaining <= 0f) return;

        float amount = Mathf.Min(singleSkillCostTick * Time.deltaTime, remaining, SkillMeter);
        if (amount <= 0f) return;

        SkillMeter -= amount;
        singleSkillChargeCost += amount;
        RaiseEnergyChanged();
    }

    private void PayRemainingSkillCost()
    {
        float remaining = Mathf.Max(0f, SkillActivationCost - singleSkillChargeCost);
        singleSkillChargeCost = 0f;

        if (skillMeterAlwaysFull) return;

        SkillMeter = Mathf.Max(0f, SkillMeter - remaining);
        RaiseEnergyChanged();
    }

    private void RefundChargeCost()
    {
        if (singleSkillChargeCost <= 0f) return;

        float refund = singleSkillChargeCost;
        singleSkillChargeCost = 0f;

        if (skillMeterAlwaysFull) return;
        ChargeSkillMeter(refund);
    }

    private static float Tick(float timer, float dt) => timer > 0f ? timer - dt : timer;
    private static float HoldOrTick(bool active, float resetValue, float current, float dt) =>
        active ? resetValue : current - dt;

    private void UpdateTimers()
    {
        parryBufferTimer = Tick(parryBufferTimer, Time.deltaTime);
        attackBufferTimer = Tick(attackBufferTimer, Time.deltaTime);
        skillBufferTimer = Tick(skillBufferTimer, Time.deltaTime);
        dashAttackBlockTimer = Tick(dashAttackBlockTimer, Time.deltaTime);
        jumpAttackBlockTimer = Tick(jumpAttackBlockTimer, Time.deltaTime);
        plungeGraceTimer = Tick(plungeGraceTimer, Time.deltaTime);
        plungeRecoveryTimer = Tick(plungeRecoveryTimer, Time.deltaTime);
        passiveEnergyRecoveryTimer = Tick(passiveEnergyRecoveryTimer, Time.deltaTime);
        parryWindowExtensionTimer = Tick(parryWindowExtensionTimer, Time.deltaTime);

        attackTimer = HoldOrTick(isAttacking, EffectiveAttackCooldown, attackTimer, Time.deltaTime);
        skillTimer = HoldOrTick(isSkilling, skillCoolDown, skillTimer, Time.deltaTime);

        if (isAttacking)
        {
            attackDurationTimer -= Time.deltaTime;
            if (attackDurationTimer <= 0f)
            {
                EndAttack();
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
                parryWindowExtensionTimer = parryWindowExtension;
            }
        }

        if (isParryInRecovery)
        {
            parryRecoveryTimer -= Time.deltaTime;
            if (parryRecoveryTimer <= 0f)
            {
                isParryInRecovery = false;
                movement.FreezeMovement(false);
            }
        }

        if (isAttacking || isSkilling || isChargingSkill || player.Controller.IsDashing)
            ResetPassiveEnergyTimer();
        if (passiveEnergyRecoveryTimer <= 0f && SkillMeter < passiveEnergyCap)
            ChargeSkillMeter(Mathf.Min(Time.deltaTime * passiveEnergyRegenMult, passiveEnergyCap - SkillMeter));
    }

    private bool CanActBase()
    {
        return movement.InputEnabled
            && !movement.IsWallSliding
            && !isParryInRecovery
            && !isSkilling
            && !isPlunging
            && !movement.IsHealing
            && !movement.IsClimbing;
    }

    private bool CanAct()
    {
        return CanActBase()
               && !movement.IsChargingSkill
               && !isChargingSkill
               && !movement.IsNeutralDash;
    }

    private bool CanReleaseSkill()
    {
        return CanActBase() && !isParrying;
    }

    public bool CanHeal =>
        movement.InputEnabled
        && movement.IsGrounded
        && !movement.IsDashing
        && !movement.IsWallSliding
        && !movement.IsClimbing
        && !movement.IsBouncing
        && !movement.IsChargingSkill
        && !isAttacking
        && !comboQueued
        && attackBufferTimer <= 0f
        && !isParrying
        && !isParryInRecovery
        && parryBufferTimer <= 0f
        && !isSkilling
        && !isChargingSkill
        && !skillButtonHeld
        && skillBufferTimer <= 0f
        && !isPlunging
        && plungeRecoveryTimer <= 0f
        && !movement.IsHealing;

    public void CancelAllActions()
    {
        movement.CancelHeal();
        CancelParry();
        ForceCancelAttack();
        CancelSkillStates();
        skillBufferTimer = 0f;
        skillPressed = false;
    }

    #region Parry Logic

    private void HandleParry()
    {
        if (isParrying || isParryInRecovery) return;
        if (parryBufferTimer > 0f && CanAct()) ExecuteParry();
    }

    private void ExecuteParry()
    {
        ForceCancelAttack();

        if (parrySuccessResetCoroutine != null)
        {
            StopCoroutine(parrySuccessResetCoroutine);
            parrySuccessResetCoroutine = null;
        }
        isParrySuccess = false;

        parryBufferTimer = 0f;
        isParrying = true;
        isParryInRecovery = false;
        parryActiveTimer = parryActiveDuration;
        parryWindowExtensionTimer = 0f;
        movement.FreezeMovement(true);
        parryDir = GetInputDirection();
    }

    private ParryDirection GetInputDirection()
    {
        if (movement.IsUpIntent) return ParryDirection.Up;
        if (movement.IsDownIntent && !movement.IsGrounded) return ParryDirection.Down;
        return movement.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;
    }

    public bool CheckParry(ParryDirection incomingDirection, bool ignoreDirection = false)
    {
        bool directionMatches = ignoreDirection || parryDir == incomingDirection || !movement.IsGrounded;
        bool windowOpen = isParrying || parryWindowExtensionTimer > 0f;
        if (!windowOpen || !directionMatches) return false;
        OnSuccessfulParry();
        return true;
    }

    private void OnSuccessfulParry()
    {
        OnParrySuccess?.Invoke();
        player.Animation.FlashGreenOnParrySuccess();
        CancelParry();
        AudioManager.PlaySFX(parryEvent, transform.position);
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
        bool wasParrying = isParrying || isParryInRecovery;

        if (parrySuccessResetCoroutine != null)
        {
            StopCoroutine(parrySuccessResetCoroutine);
            parrySuccessResetCoroutine = null;
        }

        isParrying = isParryInRecovery = isParrySuccess = false;
        parryActiveTimer = parryRecoveryTimer = 0f;
        parryBufferTimer = 0f;
        parryWindowExtensionTimer = 0f;

        if (wasParrying) movement.FreezeMovement(false);
    }

    #endregion

    #region Attack & Combo Logic

    private void HandleAttack()
    {
        if (dashAttackBlockTimer > 0f) return;
        if (jumpAttackBlockTimer > 0f) return;

        bool inComboGrace = !isAttacking && comboGraceTimer > 0f;

        if (attackBufferTimer > 0f && CanAct() && ((isAttacking && canBufferNextCombo) || inComboGrace))
        {
            comboQueued = true;
            attackBufferTimer = 0f;
        }

        if (attackBufferTimer > 0f && CanAct()) ExecuteAttack();
        if (comboQueued && !isAttacking && attackTimer <= 0f && CanAct()) ExecuteAttack();

        if (!isAttacking && comboGraceTimer > 0f && (!comboQueued || attackTimer <= 0f))
        {
            comboGraceTimer -= Time.deltaTime;
            if (comboGraceTimer <= 0f) ResetCombo();
        }
    }

    private void ExecuteAttack()
    {
        attackBufferTimer = 0f;

        if ((canBufferNextCombo && comboQueued) || (!isAttacking && attackTimer <= 0f && CanAct()))
        {
            CancelParry();
            comboQueued = false;
            canBufferNextCombo = false;
            comboGraceTimer = 0f;

            if (player.Equipment.EquippedWeapon == null) return;

            currentComboIndex = (currentComboIndex % maxComboCount) + 1;

            isAttacking = true;
            AttackSerial++;
            attackStartAirTime = airTime;
            attackStartedGrounded = movement.IsGrounded;
            isDashAttack = movement.IsDashing;
            attackDurationTimer = attackDuration / AttackSpeedMultiplier;
            attackTotalDuration = attackDurationTimer;

            enemiesHitThisAttack.Clear();
            passiveTriggeredThisAttack = false;

            if (movement.IsDashing)
            {
                movement.SetDashLockedDuringAttack(true);
            }
        }
    }

    private bool IsAttackHitActive()
    {
        if (attackTotalDuration <= 0f) return true;

        float progress = 1f - attackDurationTimer / attackTotalDuration;
        return progress >= attackActiveStart && progress <= attackActiveEnd;
    }

    private void PerformAttackHitboxCheck()
    {
        if (isPlunging) return;

        Vector2 attackDir = GetInputDirection() switch
        {
            ParryDirection.Up => Vector2.up,
            ParryDirection.Down => Vector2.down,
            ParryDirection.Left => Vector2.left,
            _ => Vector2.right
        };

        if (attackDir == Vector2.down && attackStartAirTime < minPlungeAirTime)
            attackDir = movement.FacingDirection == 1 ? Vector2.right : Vector2.left;

        float weaponRange = player.Equipment.EquippedWeapon.InstRolledRange + (HasAttackGem() ? attackGemRangeBonus : 0f);

        if (attackDir != Vector2.down && airAttackAllAround && !attackStartedGrounded && !isDashAttack)
        {
            if (!IsAttackHitActive()) return;

            Bounds body = movement.BodyBounds;
            attackCenter = (Vector2)body.center + airAttackCenterOffset;
            lastAttackRadius = weaponRange * airAttackRadiusMultiplier;
            attackRange = Vector2.one * lastAttackRadius * 2f;
            lastAttackWasCircle = true;

            Collider2D[] enemiesAround = Physics2D.OverlapCircleAll(attackCenter, lastAttackRadius, enemyLayer);
            if (enemiesAround.Length > 0)
            {
                AttackContext context = player.Equipment.GetModifiedAttackContext(isAttack: true);
                HitEnemy(enemiesAround, context);
            }
        }
        else if (attackDir != Vector2.down)
        {
            if (!IsAttackHitActive()) return;

            lastAttackWasCircle = false;
            bool isHorizontal = attackDir.x != 0f;

            attackRange = isHorizontal ? new Vector2(weaponRange, attackWidth) : new Vector2(attackWidth, weaponRange);
            attackCenter = (Vector2)attackOrigin.position + attackDir * (weaponRange * 0.5f);
            ClampAttackToBodyBottom();
            if (isHorizontal) ClampAttackToBodyTop();

            Collider2D[] enemiesInRange = Physics2D.OverlapBoxAll(attackCenter, attackRange, 0f, enemyLayer);
            if (enemiesInRange.Length > 0)
            {
                AttackContext context = player.Equipment.GetModifiedAttackContext(isAttack: true);
                HitEnemy(enemiesInRange, context);
            }
        }
        else
        {
            if (plungeRecoveryTimer <= 0f)
            {
                StartPlunge(weaponRange);
            }
        }
    }

    private void StartPlunge(float weaponRange)
    {
        CancelParry();
        CancelSkillStates();
        movement.CancelDash();

        parryBufferTimer = 0f;
        attackBufferTimer = 0f;
        skillBufferTimer = 0f;
        comboQueued = false;
        canBufferNextCombo = false;

        plungeCoroutine = StartCoroutine(PlungeAttack(weaponRange));
    }

    private IEnumerator PlungeAttack(float weaponRange)
    {
        isPlunging = true;

        float plungeTimer = 0f;
        attackRange = new Vector2(attackWidth, weaponRange);
        lastAttackWasCircle = false;

        while (isPlunging)
        {
            plungeTimer += Time.deltaTime;
            attackDurationTimer = attackDuration;
            attackCenter = (Vector2)attackOrigin.position + Vector2.down * (weaponRange * 0.5f);
            Collider2D[] enemiesInRange = Physics2D.OverlapBoxAll(attackCenter, attackRange, 0f, enemyLayer);

            Collider2D[] validHurtboxes = FilterHurtboxes(enemiesInRange);

            if (validHurtboxes.Length > 0)
            {
                isPlunging = false;
                PlungeHitCount++;
                plungeGraceTimer = plungeGraceWindow;
                plungeRecoveryTimer = plungeRecoveryDuration;

                float adjusted = plungeTimer * plungeAdjustedDmg;
                float plungeDmgMultiplier = Mathf.Clamp(adjusted, 0f, plungeDmgMaxMultiplier);
                AttackContext context = player.Equipment.GetModifiedAttackContext(isAttack: true);
                HitEnemy(validHurtboxes, context, plungeDmgMultiplier);
                Vector2 bouncePoint = GetClosestBouncePoint(validHurtboxes);
                float side = transform.position.x >= bouncePoint.x ? 1f : -1f;
                Vector2 bounceDir = new Vector2(side * plungeBounceSideAmount, 1f).normalized;
                movement.ApplyBounceImpulseInDirection(bounceDir, plungeBounceForce);
                movement.PlayBounceState(movement.BounceDuration);
            }
            else if (player.Controller.IsGrounded)
            {
                isPlunging = false;
                plungeGraceTimer = plungeGraceWindow;
                plungeRecoveryTimer = plungeRecoveryDuration;
                if (player.Animation != null) player.Animation.TriggerPlungeLandEffect();
            }

            yield return null;
        }

        plungeCoroutine = null;
        EndAttack();
    }

    private void ClampAttackToBodyBottom()
    {
        if (!clampAttackToBodyBottom || movement == null) return;

        float minY = movement.BodyBounds.min.y + attackBottomPadding;
        float bottom = attackCenter.y - attackRange.y * 0.5f;
        if (bottom >= minY) return;

        float top = attackCenter.y + attackRange.y * 0.5f;
        bottom = Mathf.Min(minY, top - 0.01f);
        attackRange.y = top - bottom;
        attackCenter.y = (top + bottom) * 0.5f;
    }

    private void ClampAttackToBodyTop()
    {
        if (!clampAttackToBodyTop || movement == null) return;

        float maxY = movement.BodyBounds.max.y + attackTopPadding;
        float top = attackCenter.y + attackRange.y * 0.5f;
        if (top <= maxY) return;

        float bottom = attackCenter.y - attackRange.y * 0.5f;
        top = Mathf.Max(maxY, bottom + 0.01f);
        attackRange.y = top - bottom;
        attackCenter.y = (top + bottom) * 0.5f;
    }

    private bool IsAttackBlocked(Collider2D target)
    {
        if (!blockAttacksThroughWalls || attackBlockMask.value == 0 || movement == null) return false;

        Bounds body = movement.BodyBounds;
        Vector2 targetPoint = target.ClosestPoint(body.center);
        float inset = Mathf.Min(0.1f, body.extents.y * 0.5f);
        Vector2 start = new Vector2(body.center.x, Mathf.Clamp(targetPoint.y, body.min.y + inset, body.max.y - inset));
        if ((targetPoint - start).sqrMagnitude < 0.0001f) return false;

        ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = attackBlockMask };
        int count = Physics2D.Linecast(start, targetPoint, filter, attackBlockHits);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = attackBlockHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform.root)) continue;
            if (hit.transform.root == target.transform.root) continue;
            if (IsPassThroughBlocker(hit)) continue;
            if (debugAttackReach)
            {
                Debug.DrawLine(start, attackBlockHits[i].point, Color.red, debugLineDuration);
                Debug.DrawLine(attackBlockHits[i].point, targetPoint, new Color(1f, 0.5f, 0f), debugLineDuration);
            }
            return true;
        }

        if (debugAttackReach) Debug.DrawLine(start, targetPoint, Color.green, debugLineDuration);
        return false;
    }

    private static bool IsPassThroughBlocker(Collider2D col)
    {
        return col.usedByEffector
               || col.GetComponentInParent<PlatformEffector2D>() != null
               || col.GetComponentInParent<DropThroughPlatform>() != null
               || col.GetComponentInParent<AirOnlyCollisionPlatform>() != null;
    }

    private Collider2D[] FilterHurtboxes(Collider2D[] colliders)
    {
        List<Collider2D> valid = new List<Collider2D>();
        foreach (var col in colliders)
        {
            if (col.CompareTag("EnemyHurtBox") && col.transform.root.TryGetComponent(out EnemyHealth _) && !IsAttackBlocked(col))
            {
                valid.Add(col);
            }
        }
        return valid.ToArray();
    }

    private Vector2 GetClosestBouncePoint(Collider2D[] enemiesInRange)
    {
        Vector2 origin = attackCenter;
        Vector2 closest = enemiesInRange[0].transform.position;
        float closestDist = ((Vector2)enemiesInRange[0].transform.position - origin).sqrMagnitude;

        for (int i = 1; i < enemiesInRange.Length; i++)
        {
            float dist = ((Vector2)enemiesInRange[i].transform.position - origin).sqrMagnitude;
            if (dist < closestDist)
            {
                closest = enemiesInRange[i].transform.position;
                closestDist = dist;
            }
        }

        return closest;
    }

    public void CancelPlunge()
    {
        if (plungeCoroutine != null)
        {
            StopCoroutine(plungeCoroutine);
            plungeCoroutine = null;
        }

        if (isPlunging)
        {
            plungeRecoveryTimer = plungeRecoveryDuration;
        }

        isPlunging = false;
    }

    private float GetComboMultiplier()
    {
        int multiplierIndex = Mathf.Clamp(currentComboIndex - 1, 0, comboDamageMultipliers.Length - 1);
        return comboDamageMultipliers[multiplierIndex];
    }

    private void HitEnemy(Collider2D[] enemiesInRange, AttackContext context, float plungeDmgMult = 0f)
    {
        float attackDamage = context.BaseAttackDamage * GetComboMultiplier() * (1f + GetAttackModifierBonus());
        context.IsComboFinisher = plungeDmgMult <= 0f && currentComboIndex >= maxComboCount;

        if (plungeDmgMult <= 0f)
        {
            if (isDashAttack) attackDamage *= dashAttackMultiplier + (HasAttackGem() ? attackGemDashMultiplierBonus : 0f);
            if (!attackStartedGrounded) attackDamage *= airAttackMultiplier;
        }

        foreach (var col in enemiesInRange)
        {
            if (col.CompareTag("EnemyHurtBox") && col.transform.root.TryGetComponent(out EnemyHealth enemyHealth))
            {
                if (!enemiesHitThisAttack.Contains(enemyHealth) && !IsAttackBlocked(col))
                {
                    enemiesHitThisAttack.Add(enemyHealth);

                    float dmg = plungeDmgMult > 0f
                        ? attackDamage * (1 + plungeDmgMult + (HasAttackGem() ? attackGemPlungeMultiplierBonus : 0f))
                        : attackDamage * (isCounterAttacking ? counterAttackMultiplier : 1f);

                    float roll = UnityEngine.Random.value;
                    bool isCrit = roll < context.BaseAttackCrit;
                    dmg *= (isCrit ? critDamageMultiplier : 1f);

                    enemyHealth.ApplyHit((int)dmg, context, isCrit);
                }
            }
        }

        if (passiveTriggeredThisAttack) return;
        passiveTriggeredThisAttack = true;
        CheckEnergyChargePassive(isAttack: true, context);
    }

    public float GetScaledAttackDamage()
    {
        float weaponDamage = player.Equipment.EquippedWeapon != null
            ? player.Equipment.EquippedWeapon.InstRolledDamage
            : 0f;

        float attackStat = player.Stats.TotalAttack;
        float scaledAttack = Mathf.Pow(attackStat, damageScalingExponent);

        return weaponDamage + scaledAttack;
    }

    public void EndAttack()
    {
        isAttacking = false;
        attackDurationTimer = 0f;
        canBufferNextCombo = false;

        if (!comboQueued)
        {
            if (comboGraceTime > 0f && currentComboIndex > 0 && currentComboIndex < maxComboCount)
                comboGraceTimer = comboGraceTime;
            else
                ResetCombo();
        }

        movement.SetDashLockedDuringAttack(false);
    }

    private void ResetCombo()
    {
        currentComboIndex = 0;
        comboQueued = false;
        canBufferNextCombo = false;
        comboGraceTimer = 0f;
    }

    #endregion

    #region Skill Logic

    private void HandleSkill()
    {
        SkillActivationCost = currentSkillDef != null ? currentSkillDef.SkillCost : 0f;
        float requiredEnergy = Mathf.Max(0f, SkillActivationCost - singleSkillChargeCost);
        bool isReadyToFire = skillMeterAlwaysFull || SkillMeter >= requiredEnergy;
        if (skillBufferTimer > 0f && CanReleaseSkill() && isReadyToFire)
        {
            ExecuteSkill();
        }
    }

    public void ChargeSkillMeter(float amount)
    {
        SkillMeter = Mathf.Clamp01(SkillMeter + amount);
        RaiseEnergyChanged();
    }

    public void ResetPassiveEnergyTimer()
    {
        passiveEnergyRecoveryTimer = passiveEnergyCooldown;
    }

    private void ExecuteSkill()
    {
        skillBufferTimer = 0f;

        if (!skillPressed || !(skillTimer <= 0f) || !CanReleaseSkill()) return;

        skillPressed = false;
        CancelParry();
        ForceCancelAttack();

        var specialDef = player.Equipment.SpecialAttackDef;

        if (specialDef == null)
        {
            movement.SetSkillCharging(false);
            return;
        }

        isSkilling = true;
        CurrentSkillGemName = specialDef.GemName;
        activeSkillExecutionType = specialDef.SkillExecutionType;

        bool wasCharged = chargingSkillTimer >= chargingSkillMinDur;
        float chargeDamageMultiplier = 1f;
        float chargeRatio = 0f;

        if (wasCharged)
        {
            chargeRatio = Mathf.InverseLerp(chargingSkillMinDur, chargingSkillMaxDur, chargingSkillTimer);
            chargeDamageMultiplier = Mathf.Lerp(1f, fullChargeDamageMultiplier, chargeRatio);
        }

        movement.SetSkillCharging(false);

        if (specialDef.SkillExecutionType != SkillExecutionType.Held)
        {
            movement.SetSkillGravityZero(true);
        }

        if (specialDef.SkillExecutionType == SkillExecutionType.Held)
        {
            skillCoroutine = StartCoroutine(PerformTimedSkill(specialDef));
        }
        else
        {
            if (specialDef.SkillType == SkillType.Single)
            {
                PerformSingleSkill(specialDef, chargeRatio, chargeDamageMultiplier);
            }
            else if (specialDef.SkillType == SkillType.Timed)
            {
                skillCoroutine = StartCoroutine(PerformTimedSkill(specialDef, chargeDamageMultiplier, chargeRatio));
            }
        }
    }

    private void PerformSingleSkill(PrimaryGemBehaviourDefinition def, float chargePercentage, float multiplier)
    {
        PayRemainingSkillCost();
        AttackContext context = player.Equipment.GetModifiedAttackContext(isAttack: false);
        context.DamageType = EDamageType.Skill;
        context.SkillModifierBonus = GetSkillModifierBonus();
        def.Execute(context, context.BaseAttackDamage * multiplier, chargePercentage);
    }

    private IEnumerator PerformTimedSkill(PrimaryGemBehaviourDefinition def, float fixedChargeMultiplier = 1f, float chargePercentage = 0f)
    {
        float tick = def.EnergyDrainTick > 0f ? def.EnergyDrainTick : DefaultEnergyDrainTick;

        bool isHeld = def.SkillExecutionType == SkillExecutionType.Held;

        if (!isHeld) PayRemainingSkillCost();
        heldRampStartTime = -1f;
        bool firstTickDone = false;

        while (isSkilling)
        {
            if (isHeld && !skillMeterAlwaysFull && SkillMeter < def.SkillCost) break;

            float dynamicRampMultiplier = fixedChargeMultiplier;
            float currentChargePercentage = chargePercentage;
            float currentTick = tick;

            if (isHeld)
            {
                float chargeRatio = HeldSkillRamp;
                currentTick = tick * Mathf.Lerp(1f, heldSkillFastestTickMultiplier, chargeRatio);

                if (!skillMeterAlwaysFull)
                {
                    SkillMeter = Mathf.Max(0f, SkillMeter - def.SkillCost);
                    RaiseEnergyChanged();
                }

                dynamicRampMultiplier = Mathf.Lerp(1f, fullChargeDamageMultiplier, chargeRatio);
                currentChargePercentage = chargeRatio;
            }

            AttackContext context = player.Equipment.GetModifiedAttackContext(isAttack: false);
            context.DamageType = EDamageType.Skill;
            context.SkillModifierBonus = GetSkillModifierBonus();

            float currentTickDamage = context.BaseAttackDamage * dynamicRampMultiplier;
            def.Execute(context, currentTickDamage, currentChargePercentage);

            yield return new WaitForSeconds(currentTick);

            if (isHeld && !firstTickDone)
            {
                firstTickDone = true;
                heldRampStartTime = Time.time;
            }
        }

        EndSkill();
    }

    private void StopSkillCoroutine()
    {
        if (skillCoroutine != null)
        {
            StopCoroutine(skillCoroutine);
            skillCoroutine = null;
        }
    }

    public void EndSkill()
    {
        StopSkillCoroutine();

        isSkilling = isChargingSkill = skillButtonHeld = false;
        heldRampStartTime = -1f;
        CurrentSkillGemName = string.Empty;
        movement.SetSkillGravityZero(false);
        StopChargingSound();
    }

    public void CancelSkillStates()
    {
        StopSkillCoroutine();
        RefundChargeCost();
        heldRampStartTime = -1f;

        isChargingSkill = false;
        isSkilling = false;
        skillButtonHeld = false;
        skillFiredThisHold = true;
        CurrentSkillGemName = string.Empty;

        CancelInvoke(nameof(AutoFireAtMaxCharge));
        StopChargingSound();

        movement.SetSkillCharging(false);
        movement.SetSkillGravityZero(false);
    }

    #endregion

    #region SecondaryGem Logic

    public bool HasAttackHyperArmor =>
        (attackGemHyperArmor && (isAttacking || isSkilling || isChargingSkill) && HasAttackGem())
        || HasSkillComboHyperArmor;

    public bool HasSkillComboHyperArmor => (isSkilling || isChargingSkill) && MatchesSkillHyperArmorCombo();

    private bool MatchesSkillHyperArmorCombo()
    {
        if (skillHyperArmorCombos == null || skillHyperArmorCombos.Length == 0 || player == null || player.Equipment == null) return false;

        PrimaryGemBehaviourDefinition skill = player.Equipment.SpecialAttackDef;
        SecondaryGemInstance gem = player.Equipment.SecondaryGem;
        if (skill == null || gem == null || string.IsNullOrEmpty(gem.InstTemplateID)) return false;

        SecondaryGemBehaviourDefinition gemDef = GameDatabase.GetSecondaryTemplateFromID(gem.InstTemplateID);
        if (gemDef == null) return false;

        foreach (SkillHyperArmorCombo combo in skillHyperArmorCombos)
        {
            if (combo != null && combo.skillGem == skill && combo.secondaryGem == gemDef) return true;
        }

        return false;
    }

    private bool HasAttackGem() => HasGemOfType(SGemType.Attack);

    public float AttackSpeedMultiplier => HasAttackGem() ? 1f + attackGemAttackSpeedBonus : 1f;

    private float EffectiveAttackCooldown => HasAttackGem() ? attackCoolDown * (1f - attackGemCooldownReduction) : attackCoolDown;

    private bool HasGemOfType(SGemType type)
    {
        SecondaryGemInstance gem = player != null && player.Equipment != null ? player.Equipment.SecondaryGem : null;
        return gem != null && !string.IsNullOrEmpty(gem.InstTemplateID) && gem.Type == type;
    }

    private float GetAttackModifierBonus()
    {
        SecondaryGemInstance gem = player.Equipment.SecondaryGem;
        bool hasAttackGem = gem != null && !string.IsNullOrEmpty(gem.InstTemplateID) && gem.Type == SGemType.Attack;
        return hasAttackGem ? attackGemModifierBonus : 0f;
    }

    private float GetSkillModifierBonus()
    {
        SecondaryGemInstance gem = player.Equipment.SecondaryGem;
        bool hasSkillGem = gem != null && !string.IsNullOrEmpty(gem.InstTemplateID) && gem.Type == SGemType.Skill;
        return hasSkillGem ? skillGemModifierBonus : 0f;
    }

    public void CheckEnergyChargePassive(bool isAttack, AttackContext context)
    {
        if ((player.Equipment.SecondaryGem.Type == SGemType.Attack && isAttack)
            || player.Equipment.SecondaryGem.Type == SGemType.Skill && !isAttack)
        {
            GainGemEnergy(context.ChargeAmount);
        }
    }

    private void GainGemEnergy(float amount)
    {
        if (amount <= 0f) return;

        ChargeSkillMeter(amount);
        OnGemEnergyGained?.Invoke(amount);
        AnyGemEnergyGained?.Invoke(this, amount);
    }

    public bool CanParryHeavy
    {
        get
        {
            if (!reflectGemParriesHeavy || player == null || player.Equipment == null) return false;

            var secondaryGem = player.Equipment.SecondaryGem;
            if (secondaryGem == null || string.IsNullOrEmpty(secondaryGem.InstTemplateID) || secondaryGem.Type != SGemType.Parry)
                return false;

            var secondaryDef = GameDatabase.GetSecondaryTemplateFromID(secondaryGem.InstTemplateID);
            return secondaryDef != null && secondaryDef.GetPassiveType(secondaryGem) == PassiveType.Reflect;
        }
    }

    public void TryModifyParry(float incomingDamage, Collider2D col)
    {
        var secondaryGem = player.Equipment.SecondaryGem;

        if (secondaryGem == null || string.IsNullOrEmpty(secondaryGem.InstTemplateID) || secondaryGem.Type != SGemType.Parry)
            return;

        var secondaryDef = GameDatabase.GetSecondaryTemplateFromID(secondaryGem.InstTemplateID);
        if (secondaryDef == null) return;

        switch (secondaryDef.GetPassiveType(secondaryGem))
        {
            case PassiveType.Energy:
                GainGemEnergy(secondaryGem.InstRolledChargeAmount);
                break;
            case PassiveType.Reflect:
                TryReflectDmg(secondaryDef, col, incomingDamage);
                break;
            case PassiveType.DoT:
                Debug.Log("parry trying triggering DoT passive");
                break;
            default:
                Debug.Log("secondary gem passive type not implemented for parries");
                break;
        }
    }

    private float GetDefenseReflectMultiplier()
    {
        float defense = player != null && player.Stats != null ? player.Stats.TotalDefense : 0f;
        return Mathf.Min(1f + Mathf.Max(0f, defense) * reflectBonusPerDefense, maxDefenseReflectMultiplier);
    }

    private static EnemyHealth ResolveReflectTarget(Collider2D col)
    {
        var projectile = col.GetComponentInParent<Enemies.ProjectileScripts.Projectile>();
        if (projectile != null)
        {
            if (projectile.Owner == null) return null;
            EnemyHealth ownerHealth = projectile.Owner.GetComponentInParent<EnemyHealth>();
            return ownerHealth != null ? ownerHealth : projectile.Owner.root.GetComponentInChildren<EnemyHealth>();
        }

        if (col.transform.root.TryGetComponent(out EnemyHealth rootHealth)) return rootHealth;
        return col.GetComponentInParent<EnemyHealth>();
    }

    private void TryReflectDmg(SecondaryGemBehaviourDefinition behaviourDefinition, Collider2D col, float incomingDamage)
    {
        if (col == null)
        {
            Debug.Log("no collider provided, no reflect damage");
            return;
        }
        EnemyHealth enemyHealth = ResolveReflectTarget(col);
        if (enemyHealth != null)
        {
            AttackContext context = new AttackContext
            {
                BaseAttackDamage = incomingDamage,
                DamageType = EDamageType.Reflect
            };

            behaviourDefinition.Modify(ref context, player.Equipment.SecondaryGem);
            context.BaseAttackDamage *= GetDefenseReflectMultiplier();
            int reflectDmg = Mathf.Max(1, Mathf.RoundToInt(context.BaseAttackDamage));
            enemyHealth.ApplyHit(reflectDmg, context);
        }
        else
        {
            Debug.Log("can't see enemy health, no reflect damage");
        }
    }

    #endregion

    #region Input Handlers & Animator Hooks

    public void OnParry()
    {
        if (movement.IsHealing || isPlunging || movement.IsUILocked || movement.IsClimbing) return;
        parryBufferTimer = parryBufferTime;
    }

    public void OnAttack()
    {
        if (movement.IsHealing || isChargingSkill || isPlunging || movement.IsUILocked || movement.IsClimbing) return;

        if (IsParrying)
        {
            CancelParry();
        }

        attackBufferTimer = attackBufferTime;

        if ((isAttacking && canBufferNextCombo) || (!isAttacking && comboGraceTimer > 0f))
        {
            comboQueued = true;
        }
    }

    public void NotifyDashInputReceived()
    {
        dashAttackBlockTimer = dashAttackConflictWindow;
    }

    public void NotifyJumpInputReceived()
    {
        jumpAttackBlockTimer = jumpAttackConflictWindow;
    }

    public void OnSAttack(InputValue value)
    {
        var specialDef = player.Equipment?.SpecialAttackDef;

        if (value.isPressed)
        {
            if (movement.IsHealing || isPlunging || movement.IsUILocked || movement.IsClimbing) return;
            if (movement.IsWallSliding) return;
            if (specialDef == null) return;

            RefundChargeCost();
            if (!skillMeterAlwaysFull && SkillMeter < specialDef.SkillCost) return;

            skillButtonHeld = true;
            chargingSkillTimer = 0f;
            skillFiredThisHold = false;

            CancelParry();
            ForceCancelAttack();

            if (specialDef.SkillExecutionType == SkillExecutionType.Held)
            {
                TriggerSkillRelease();
                return;
            }

            isChargingSkill = true;
            StopChargingSound();
            chargingEventInstance = AudioManager.StartVariableLengthSFX(chargingSkillEvent,gameObject);
            singleSkillCostTick = specialDef.SkillCost / chargingSkillMaxDur;
            CancelInvoke(nameof(AutoFireAtMaxCharge));
            Invoke(nameof(AutoFireAtMaxCharge), chargingSkillMaxDur);
        }
        else
        {
            bool wasHeld = skillButtonHeld;
            skillButtonHeld = false;
            if (!wasHeld) return;

            if (movement.IsUILocked && !isSkilling)
            {
                isChargingSkill = false;
                skillFiredThisHold = true;
                StopChargingPhysics();
                RefundChargeCost();
                return;
            }

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

            if (!skillFiredThisHold && !movement.IsClimbing) TriggerSkillRelease();
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

    private void AutoFireAtMaxCharge() => FireChargedSkill(false, chargingSkillMaxDur);

    private void FireChargedSkill(bool triggeredEarly, float chargingDur)
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
        movement.SetSkillCharging(false);
        CancelInvoke(nameof(AutoFireAtMaxCharge));
        StopChargingSound();
    }

    private void StopChargingSound()
    {
        if (!chargingEventInstance.isValid()) return;

        AudioManager.StopandReleaseVariableLengthSFX(chargingEventInstance);
        chargingEventInstance = default;
    }

    public void ForceCancelAttack()
    {
        isAttacking = false;
        CancelPlunge();
        attackDurationTimer = 0f;
        enemiesHitThisAttack.Clear();
        ResetCombo();

        attackBufferTimer = 0f;

        movement.SetDashLockedDuringAttack(false);
    }

    public void OpenComboWindow()
    {
        if (!isAttacking) return;
        canBufferNextCombo = true;
    }

    public void CloseComboWindow()
    {
        if (!useComboCloseEvent) return;
        canBufferNextCombo = false;
    }

    #endregion

    private void OnDisable()
    {
        StopChargingSound();
    }

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null || debugAttackReach) return;
        DrawAttackGizmo();
    }

    private void OnDrawGizmos()
    {
        if (!debugAttackReach || attackOrigin == null) return;

        if (Application.isPlaying) DrawAttackGizmo();

        PlayerController controller = movement != null ? movement : GetComponentInParent<PlayerController>();
        if (controller == null) controller = transform.root.GetComponentInChildren<PlayerController>();
        if (controller == null) return;

        Bounds body = controller.BodyBounds;
        float weaponRange = debugPreviewRange;
        if (Application.isPlaying && player != null && player.Equipment != null && player.Equipment.EquippedWeapon != null)
            weaponRange = player.Equipment.EquippedWeapon.InstRolledRange + (HasAttackGem() ? attackGemRangeBonus : 0f);
        float left = body.center.x - weaponRange - body.extents.x;
        float right = body.center.x + weaponRange + body.extents.x;

        if (clampAttackToBodyBottom)
        {
            float bottom = body.min.y + attackBottomPadding;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(left, bottom), new Vector3(right, bottom));
        }

        if (clampAttackToBodyTop)
        {
            float top = body.max.y + attackTopPadding;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector3(left, top), new Vector3(right, top));
        }

        if (airAttackAllAround)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
            Gizmos.DrawWireSphere((Vector2)body.center + airAttackCenterOffset, weaponRange * airAttackRadiusMultiplier);
        }
    }

    private void DrawAttackGizmo()
    {
        Gizmos.color = isAttacking || isPlunging ? Color.red : new Color(1f, 0f, 0f, 0.35f);
        if (lastAttackWasCircle) Gizmos.DrawWireSphere(attackCenter, lastAttackRadius);
        else Gizmos.DrawWireCube(attackCenter, attackRange);
    }
}