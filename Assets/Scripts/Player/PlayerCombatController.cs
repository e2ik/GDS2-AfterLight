using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;

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
    [SerializeField] private float damageScalingExponent = 0.8f;
    public LayerMask enemyLayer;
    [SerializeField] private float critDamageMultiplier = 1.33f;
    [SerializeField] private float counterAttackMultiplier = 1.2f;
    [SerializeField] private float plungeDmgMaxMultiplier = float.MaxValue;
    [SerializeField] private float plungeAdjustedDmg = 1f;
    [SerializeField] private float plungeGraceWindow = 0.15f;
    [SerializeField] private float plungeBounceForce = 10f;
    [SerializeField] private float plungeRecoveryDuration = 0.3f;
    [SerializeField] private float attackWidth = 2f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackCoolDown = 0.2f;
    [SerializeField] private float attackBufferTime = 0.15f;
    [SerializeField] private float counterAttackWindow = 0.5f;

    [Header("Combo Settings")]
    [SerializeField] private int maxComboCount = 3;
    [SerializeField] private float comboResetDelay = 1.0f;

    private int currentComboIndex = 0;
    private bool canBufferNextCombo = false;
    private bool comboQueued = false;
    private float comboResetTimer;
    [SerializeField] private float[] comboDamageMultipliers = { 1.0f, 1.25f, 1.5f };
    public int CurrentComboIndex => currentComboIndex;

    [Header("Input Conflict Settings")]
    [SerializeField] private float dashAttackConflictWindow = 0.2f;
    private float dashAttackBlockTimer;
    [SerializeField] private float jumpAttackConflictWindow = 0.2f;
    private float jumpAttackBlockTimer;

    private float attackBufferTimer;
    private float attackDurationTimer;
    private Vector2 attackRange;
    private Vector2 attackCenter;
    private float attackDamage;
    private float attackCritChance;
    private float attackTimer;
    private bool isAttacking;
    private bool attackStartedGrounded;
    private bool isCounterAttacking;
    private bool isPlunging;
    private float plungeGraceTimer;
    private float plungeRecoveryTimer;

    private HashSet<EnemyHealth> enemiesHitThisAttack = new HashSet<EnemyHealth>();

    [Header("Skill Settings")]
    [SerializeField] private bool skillMeterAlwaysFull;
    [SerializeField] private float skillCoolDown = 0.2f;
    [SerializeField] private float chargingSkillMinDur = 0.4f;
    [SerializeField] private float chargingSkillMaxDur = 1.5f;
    [SerializeField] private float fullChargeDamageMultiplier = 1.5f;
    [SerializeField] private float skillHoldThreshold = 0.12f;
    [SerializeField] private float skillReleaseBufferTime = 0.08f;
    [SerializeField] private float chargeSkillAmount = 0.2f;

    [Header("FMOD Events")]
    [SerializeField] private EventReference parryEvent;

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

    private float verticalInput;
    private Player player;
    private PlayerController movement;

    public bool IsAttacking => isAttacking;
    public bool AttackStartedGrounded => attackStartedGrounded;
    public bool IsPlunging => isPlunging;
    public bool WasRecentlyPlunging => plungeGraceTimer > 0f;
    public bool IsParrying => isParrying || isParryInRecovery;
    public bool IsParrySuccess => isParrySuccess;
    public bool IsSkilling => isSkilling;
    public bool IsSkillingWithMovementLock => isSkilling && activeSkillExecutionType != SkillExecutionType.Held;
    public bool IsChargeInputHeld => isChargingSkill;
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

        HandleParry();
        HandleAttack();
        HandleSkill();
        UpdateTimers();

        if (isAttacking && player.Equipment?.EquippedWeapon != null)
        {
            PerformAttackHitboxCheck();
        }

        if (!skillButtonHeld) return;

        if (movement.IsWallSliding)
        {
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

        float maxCost = currentSkillDef.SkillCost;
        float availableCost = SkillMeter - maxCost;

        if (availableCost <= 0f || singleSkillChargeCost >= maxCost)
        {
            FireChargedSkill(true, chargingSkillTimer);
            return;
        }

        float amount = singleSkillCostTick * Time.deltaTime;
        amount = Mathf.Min(amount, maxCost - singleSkillChargeCost);

        SkillMeter -= amount;
        singleSkillChargeCost += amount;
        RaiseEnergyChanged();

        if (SkillMeter <= maxCost)
        {
            SkillMeter = currentSkillDef.SkillCost;
            RaiseEnergyChanged();
            FireChargedSkill(true, chargingSkillTimer);
        }
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

        attackTimer = HoldOrTick(isAttacking, attackCoolDown, attackTimer, Time.deltaTime);
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
    }

    private bool CanActBase()
    {
        return movement.InputEnabled
            && !movement.IsWallSliding
            && !isParryInRecovery
            && !isSkilling
            && !isPlunging;
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

    #region Parry Logic

    private void HandleParry()
    {
        if (isParrying || isParryInRecovery) return;
        if (parryBufferTimer > 0f && CanAct()) ExecuteParry();
    }

    private void ExecuteParry()
    {
        ForceCancelAttack();
        parryBufferTimer = 0f;
        isParrying = true;
        isParryInRecovery = false;
        parryActiveTimer = parryActiveDuration;
        movement.FreezeMovement(true);
        parryDir = GetInputDirection();
    }

    private ParryDirection GetInputDirection()
    {
        if (verticalInput > 0.01f) return ParryDirection.Up;
        if (verticalInput < -0.01f && !movement.IsGrounded) return ParryDirection.Down;
        return movement.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;
    }

    public bool CheckParry(ParryDirection incomingDirection)
    {
        bool directionMatches = parryDir == incomingDirection || !movement.IsGrounded;

        if (isParrying && directionMatches)
        {
            OnSuccessfulParry();
            return true;
        }
        return false;
    }

    private void OnSuccessfulParry()
    {
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
        if (parrySuccessResetCoroutine != null)
        {
            StopCoroutine(parrySuccessResetCoroutine);
            parrySuccessResetCoroutine = null;
        }

        isParrying = isParryInRecovery = isParrySuccess = false;
        parryActiveTimer = parryRecoveryTimer = 0f;
        movement.FreezeMovement(false);
    }

    #endregion

    #region Attack & Combo Logic

    private void HandleAttack()
    {
        if (dashAttackBlockTimer > 0f) return;
        if (jumpAttackBlockTimer > 0f) return;

        if (attackBufferTimer > 0f && isAttacking && canBufferNextCombo && CanAct())
        {
            comboQueued = true;
            attackBufferTimer = 0f;
        }

        if (attackBufferTimer > 0f && CanAct()) ExecuteAttack();
        if (comboQueued && !isAttacking && attackTimer <= 0f && CanAct()) ExecuteAttack();

        if (currentComboIndex <= 0 || isAttacking) return;
        comboResetTimer += Time.deltaTime;
        if (comboResetTimer >= comboResetDelay) ResetCombo();
    }

    private void ExecuteAttack()
    {
        attackBufferTimer = 0f;

        if ((canBufferNextCombo && comboQueued) || (!isAttacking && attackTimer <= 0f && CanAct()))
        {
            CancelParry();
            comboQueued = false;
            canBufferNextCombo = false;

            if (player.Equipment.EquippedWeapon == null) return;

            currentComboIndex = (currentComboIndex % maxComboCount) + 1;

            isAttacking = true;
            attackStartedGrounded = movement.IsGrounded;
            attackDurationTimer = attackDuration;

            enemiesHitThisAttack.Clear();

            if (movement.IsDashing)
            {
                movement.SetDashLockedDuringAttack(true);
            }
        }
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

        float weaponRange = player.Equipment.EquippedWeapon.InstRolledRange;
        attackDamage = GetDamage();
        attackCritChance = player.Equipment.EquippedWeapon.InstRolledCrit;

        if (attackDir != Vector2.down)
        {
            bool isHorizontal = attackDir.x != 0f;

            attackRange = isHorizontal ? new Vector2(weaponRange, attackWidth) : new Vector2(attackWidth, weaponRange);
            attackCenter = (Vector2)attackOrigin.position + attackDir * (weaponRange * 0.5f);

            Collider2D[] enemiesInRange = Physics2D.OverlapBoxAll(attackCenter, attackRange, 0f, enemyLayer);
            if (enemiesInRange.Length > 0)
            {
                AttackContext context = player.Equipment.GetModifiedAttackContext();
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
                plungeGraceTimer = plungeGraceWindow;
                plungeRecoveryTimer = plungeRecoveryDuration;

                float adjusted = plungeTimer * plungeAdjustedDmg;
                float plungeDmgMultiplier = Mathf.Clamp(adjusted, 0f, plungeDmgMaxMultiplier);
                AttackContext context = player.Equipment.GetModifiedAttackContext();
                HitEnemy(validHurtboxes, context, plungeDmgMultiplier);
                movement.ApplyBounceImpulse(GetClosestBouncePoint(validHurtboxes), plungeBounceForce);
                movement.PlayBounceState(movement.BounceDuration);
            }
            else if (player.Controller.IsGrounded)
            {
                isPlunging = false;
                plungeGraceTimer = plungeGraceWindow;
                plungeRecoveryTimer = plungeRecoveryDuration;
            }

            yield return null;
        }

        plungeCoroutine = null;
        EndAttack();
    }

    private Collider2D[] FilterHurtboxes(Collider2D[] colliders)
    {
        List<Collider2D> valid = new List<Collider2D>();
        foreach (var col in colliders)
        {
            if (col.CompareTag("EnemyHurtBox") && col.transform.root.TryGetComponent(out EnemyHealth _))
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

    private float GetDamage()
    {
        float baseDmg = GetScaledAttackDamage();

        int multiplierIndex = Mathf.Clamp(currentComboIndex - 1, 0, comboDamageMultipliers.Length - 1);
        float comboMultiplier = comboDamageMultipliers[multiplierIndex];

        return baseDmg * comboMultiplier;
    }

    private void HitEnemy(Collider2D[] enemiesInRange, AttackContext context, float plungeDmgMult = 0f)
    {
        foreach (var col in enemiesInRange)
        {
            if (col.CompareTag("EnemyHurtBox") && col.transform.root.TryGetComponent(out EnemyHealth enemyHealth))
            {
                if (!enemiesHitThisAttack.Contains(enemyHealth))
                {
                    enemiesHitThisAttack.Add(enemyHealth);

                    float dmg = plungeDmgMult > 0f
                        ? attackDamage * (1 + plungeDmgMult)
                        : attackDamage * (isCounterAttacking ? counterAttackMultiplier : 1f);

                    float roll = UnityEngine.Random.value;
                    bool isCrit = roll <= attackCritChance;
                    dmg *= (isCrit ? critDamageMultiplier : 1f);

                    enemyHealth.ApplyHit((int)dmg, context);
                }
            }
        }
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
            ResetCombo();
        }

        movement.SetDashLockedDuringAttack(false);
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
        RaiseEnergyChanged();
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
        if (wasCharged) movement.SetSkillGravityZero(true);

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
        SkillMeter -= SkillActivationCost;
        RaiseEnergyChanged();
        def.Execute(player.Equipment.GetModifiedAttackContext(), GetDamage() * multiplier, chargePercentage);
    }

    private IEnumerator PerformTimedSkill(PrimaryGemBehaviourDefinition def, float fixedChargeMultiplier = 1f, float chargePercentage = 0f)
    {
        var context = player.Equipment.GetModifiedAttackContext();
        float tick = def.EnergyDrainTick > 0f ? def.EnergyDrainTick : DefaultEnergyDrainTick;

        bool isHeld = def.SkillExecutionType == SkillExecutionType.Held;
        float energyCostPerTick = tick / chargingSkillMaxDur;

        if (!isHeld && !skillMeterAlwaysFull)
        {
            SkillMeter -= SkillActivationCost;
            RaiseEnergyChanged();
        }

        while (isSkilling && (skillMeterAlwaysFull || (isHeld ? SkillMeter > 0f : true)))
        {
            float dynamicRampMultiplier = fixedChargeMultiplier;
            float currentChargePercentage = chargePercentage;

            if (isHeld)
            {
                if (!skillMeterAlwaysFull)
                {
                    SkillMeter = Mathf.Clamp01(SkillMeter - energyCostPerTick);
                    RaiseEnergyChanged();
                }

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
        CurrentSkillGemName = string.Empty;
        movement.SetSkillGravityZero(false);
    }

    public void CancelSkillStates()
    {
        StopSkillCoroutine();

        isChargingSkill = false;
        isSkilling = false;
        skillButtonHeld = false;
        skillFiredThisHold = true;
        CurrentSkillGemName = string.Empty;

        CancelInvoke(nameof(AutoFireAtMaxCharge));

        movement.SetSkillCharging(false);
        movement.SetSkillGravityZero(false);
    }

    #endregion

    #region Input Handlers & Animator Hooks

    public void OnMove(InputValue value) => verticalInput = value.Get<Vector2>().y;

    public void OnParry()
    {
        if (isPlunging) return;
        parryBufferTimer = parryBufferTime;
    }

    public void OnAttack()
    {
        if (IsParrying || isChargingSkill || isPlunging) return;

        attackBufferTimer = attackBufferTime;

        if (isAttacking && canBufferNextCombo)
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
            if (isPlunging) return;
            if (movement.IsWallSliding) return;
            if (specialDef == null) return;
            if (!skillMeterAlwaysFull && SkillMeter <= 0f) return;

            skillButtonHeld = true;
            chargingSkillTimer = 0f;
            singleSkillChargeCost = 0f;
            skillFiredThisHold = false;

            CancelParry();
            ForceCancelAttack();

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