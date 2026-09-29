using System.Collections.Generic;
using System.Collections;
using System.Linq;
using FMODUnity;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace Enemies
{
    [RequireComponent(typeof(EnemyHealth))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private EnemyObservationSO observationSO;
        [SerializeField] private List<AttackInstance> attacks = new();
        [SerializeField] private LootTableDefinitionSO lootTable;

        [SerializeField] private BehaviorGraphAgent behaviorAgent;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D rb2D;
        [SerializeField] private float attackCooldown;
        [SerializeField] private float attackFailsafeDuration = 4f;
        [SerializeField] private float facingDeadZone = 0.15f;
        [SerializeField] private string placeholderClipName = "EmptyAttack";

        [Header("Ledge Safety")]
        [SerializeField] private bool preventLedgeFalls = true;
        [SerializeField] private bool ignoreTerrainChecks = false;
        [SerializeField] private LayerMask ledgeGroundMask;
        [SerializeField] private float ledgeCheckDistance = 0.5f;
        [SerializeField] private float ledgeCheckDepth = 1f;

        [Header("Aggro")]
        [SerializeField] private bool canDropAggro = true;
        [SerializeField] private float unreachableGiveUpDelay = 1.5f;
        [SerializeField] private float reaggroCooldown = 3f;
        private float unreachableTimer;
        private float reaggroCooldownTimer;

        [Header("Stagger")]
        [SerializeField] private bool isStaggerImmune = false;
        [SerializeField] private float staggerImmunityDuration = 2f;
        [SerializeField] private float staggerStunDuration = 0.5f;
        [SerializeField] private float postStaggerAttackDelay = 0.5f;
        private float staggerImmunityTimer;
        private float staggerStunTimer;

        public bool IsStaggered => staggerStunTimer > 0f;

        [Header("FMOD Events")]
        [SerializeField] private EventReference hitEvent;
        [SerializeField] private EventReference attackEvent;

        [Header("Damage Flash")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField] private float flashDuration = 0.15f;
        private Coroutine flashRoutine;
        private Color baseColor;

        [Header("Boss stuff")]
        [SerializeField] private BossBounds bossBounds;
        [SerializeField] private int attacksPerTeleport = 3;
        [SerializeField] private float teleportMinCooldown = 8f;
        [SerializeField] private bool canTeleport = false;
        [SerializeField] private bool teleportToTargetWhenOutOfRange = true;
        [SerializeField] private float outOfRangeTeleportDelay = 4f;
        private int attacksSinceLastTeleport;
        private float teleportCooldownTimer;
        private float outOfRangeTimer;

        public EnemyContext Context { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool AttackReady { get; private set; }
        public bool TeleportReady { get; private set; }
        public bool IsBoss => Context != null && Context.BossBounds != null;

        private float attackCooldownTimer;
        private float attackStartedTime;
        private bool wasTargetingPlayer;

        private void OnEnable()
        {
            Context.Health.OnDamageTaken += OnDamaged;
            Context.Health.OnDeath += OnDeath;
        }

        private void OnDisable()
        {
            Context.Health.OnDamageTaken -= OnDamaged;
            Context.Health.OnDeath -= OnDeath;
            EnemyCombatTracker.EnemyStoppedTargeting(this);

            if (IsAttacking) MarkAttackEnded();
        }


        private void Awake()
        {
            var overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;

            var overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overridesList);
            AnimationClip placeholderClip =
                overridesList.FirstOrDefault(pair => pair.Key.name == placeholderClipName).Key;

            if (placeholderClip == null)
                Debug.LogError($"{name}: no clip named '{placeholderClipName}' found in the base Animator Controller");

            Context = new EnemyContext()
            {
                Self = transform,
                Body = rb2D,
                Health = GetComponent<EnemyHealth>(),
                Behavior = behaviorAgent,
                Animator = animator,
                OverrideController = overrideController,
                PlaceholderClip = placeholderClip,
                BossBounds = bossBounds,
                SpriteRenderer = spriteRenderer,
                FacingRight = true
            };

            Context.AttackStopDistance = attacks.Count > 0 ? attacks.Min(a => a.Attack.Range) : 0.1f;
            Context.HomePosition = transform.position;
            Context.NavPath = new NavMeshPath();
            Context.NoiseSeed = UnityEngine.Random.value * 1000f;

            if (spriteRenderer == null)
                Debug.LogError($"{name}: no sprite renderer found");
            else
                baseColor = spriteRenderer.color;

        }

        private void Update()
        {
            Context.IgnoreTerrainChecks = ignoreTerrainChecks;

            observationSO.Tick(Context, Time.deltaTime);

            if (reaggroCooldownTimer > 0f)
            {
                reaggroCooldownTimer -= Time.deltaTime;
                Context.Target = null;
                Context.TargetVisible = false;
            }

            if (IsBoss && Context.Target != null && !IsInsideBossBounds(Context.Target.position))
                DropAggro();

            bool isTargetingPlayer = Context.TargetVisible;
            if (isTargetingPlayer != wasTargetingPlayer)
            {
                if(isTargetingPlayer)
                {
                    EnemyCombatTracker.EnemyStartedTargeting(this);
                    // can spawn aggro here
                    PSpawner.Spawn("Aggro", transform.position);
                }
                else
                    EnemyCombatTracker.EnemyStoppedTargeting(this);
                    // can spawn deaggro here

                wasTargetingPlayer = isTargetingPlayer;
            }

            if (!IsAttacking && Context.TargetVisible && Context.Target != null)
                FaceTowards(Context.Target.position.x);

            if (!IsAttacking)
                ApplyFacing();

            animator.SetFloat("Speed", Mathf.Abs(Context.Body.linearVelocityX));

            attackCooldownTimer = Mathf.Max(0, attackCooldownTimer - Time.deltaTime);
            staggerImmunityTimer = Mathf.Max(0, staggerImmunityTimer - Time.deltaTime);
            staggerStunTimer = Mathf.Max(0, staggerStunTimer - Time.deltaTime);
            teleportCooldownTimer = Mathf.Max(0, teleportCooldownTimer - Time.deltaTime);

            if (IsAttacking && Time.time - attackStartedTime >= attackFailsafeDuration)
            {
                Debug.LogWarning($"{name}: attack never reported finishing, ending it after {attackFailsafeDuration}s.");
                MarkAttackEnded();
            }

            bool attackReady = IsAttacking;
            bool usableAttackInRange = false;
            bool anyAttackInRange = false;
            bool allAttacksOnCooldown = true;

            foreach (AttackInstance attack in attacks)
            {
                attack.Tick(Context, Time.deltaTime);

                if (attack.InRange) anyAttackInRange = true;

                if (!attack.OnCooldown)
                {
                    allAttacksOnCooldown = false;
                    if (attack.InRange) usableAttackInRange = true;
                }

                if (!IsAttacking && attackCooldownTimer <= 0 && Context.CanReachTarget && attack.IsValid)
                    attackReady = true;
            }

            Context.TargetInRange = usableAttackInRange || (allAttacksOnCooldown && anyAttackInRange);

            UpdateUnreachableGiveUp();
            if (!Context.TargetVisible) attackReady = IsAttacking;

            AttackReady = attackReady;

            UpdateOutOfRangeTimer();
            bool forceTeleport = IsBoss && teleportToTargetWhenOutOfRange && outOfRangeTeleportDelay > 0f && outOfRangeTimer >= outOfRangeTeleportDelay
                                 && teleportCooldownTimer <= 0f;
            bool teleportReady = canTeleport && (forceTeleport
                || (attacksSinceLastTeleport >= attacksPerTeleport && teleportCooldownTimer <= 0f));
            Context.ForceTeleportNearTarget = canTeleport && forceTeleport;
            TeleportReady = IsBoss && teleportReady;

            behaviorAgent.BlackboardReference.SetVariableValue("TargetVisible", Context.TargetVisible);
            behaviorAgent.BlackboardReference.SetVariableValue("TargetPosition", Context.TargetPosition);
            behaviorAgent.BlackboardReference.SetVariableValue("AttackReady", attackReady);
            behaviorAgent.BlackboardReference.SetVariableValue("TeleportReady", teleportReady);
            behaviorAgent.BlackboardReference.SetVariableValue("Self", gameObject);
        }

        private void FixedUpdate()
        {
            if (IsBoss)
                KeepInsideBossBounds();

            if (!preventLedgeFalls || ledgeGroundMask == 0 || Context.IgnoreTerrainChecks) return;

            Vector2 velocity = rb2D.linearVelocity;
            if (Mathf.Abs(velocity.x) < 0.01f) return;
            if (!EnemyTerrainProbe.HasGroundBelow(Context, ledgeGroundMask, ledgeCheckDepth)) return;

            int dir = velocity.x > 0f ? 1 : -1;
            if (!EnemyTerrainProbe.HasGroundAhead(Context, dir, ledgeGroundMask, ledgeCheckDistance, ledgeCheckDepth))
            {
                rb2D.linearVelocity = new Vector2(0f, velocity.y);
            }
        }

        private void KeepInsideBossBounds()
        {
            Vector2 velocity = rb2D.linearVelocity;
            if (Mathf.Abs(velocity.x) < 0.01f) return;

            Bounds arena = Context.BossBounds.WorldBounds;
            Bounds body = EnemyTerrainProbe.GetBodyBounds(Context);
            float step = velocity.x * Time.fixedDeltaTime;

            bool leavingLeft = velocity.x < 0f && body.min.x + step < arena.min.x;
            bool leavingRight = velocity.x > 0f && body.max.x + step > arena.max.x;

            if (leavingLeft || leavingRight)
            {
                rb2D.linearVelocity = new Vector2(0f, velocity.y);
                Context.LastChaseBlockedTime = Time.time;
            }
        }

        private bool IsInsideBossBounds(Vector2 point)
        {
            Bounds arena = Context.BossBounds.WorldBounds;
            return point.x >= arena.min.x && point.x <= arena.max.x
                && point.y >= arena.min.y && point.y <= arena.max.y;
        }

        public void RunMovement(EnemyMovementSO module, float dt)
        {
            if (IsStaggered && module is not Enemies.ModuleScripts.Movement.TeleportMovementSO)
            {
                rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
                return;
            }

            module.Tick(Context, dt);
        }

        public void SetBossBounds(BossBounds bounds)
        {
            bossBounds = bounds;
            Context.BossBounds = bounds;
        }

        public bool TrySelectAttack(out AttackInstance selected)
        {
            selected = null;
            if (IsAttacking) return false;

            float totalWeight = attacks.Where(a => a.IsValid).Sum(a => a.Weight);
            if (totalWeight <= 0f) return false;

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (AttackInstance attack in attacks)
            {
                if (!attack.IsValid) continue;
                cumulative += attack.Weight;

                if (roll <= cumulative)
                {
                    selected = attack;
                    return true;
                }
            }

            return false;
        }

        private void UpdateUnreachableGiveUp()
        {
            if (!canDropAggro || IsBoss)
            {
                unreachableTimer = 0f;
                return;
            }

            bool chaseBlocked = Time.time - Context.LastChaseBlockedTime < 0.2f;
            bool unreachable = Context.TargetVisible && !IsAttacking && !Context.TargetInRange && chaseBlocked;

            if (!unreachable)
            {
                unreachableTimer = 0f;
                return;
            }

            unreachableTimer += Time.deltaTime;
            if (unreachableTimer < unreachableGiveUpDelay) return;

            DropAggro();
        }

        private void UpdateOutOfRangeTimer()
        {
            bool outOfRange = IsBoss && Context.TargetVisible && Context.Target != null
                              && !IsAttacking && !Context.TargetInRange;

            if (outOfRange)
                outOfRangeTimer += Time.deltaTime;
            else
                outOfRangeTimer = 0f;
        }

        private void DropAggro()
        {
            unreachableTimer = 0f;
            reaggroCooldownTimer = reaggroCooldown;

            Context.Target = null;
            Context.TargetVisible = false;
            Context.TimeSinceTargetSeen = 0f;
            Context.LastChaseBlockedTime = float.NegativeInfinity;
            Context.TargetInRange = false;

            EnemyCombatTracker.EnemyStoppedTargeting(this);
            wasTargetingPlayer = false;
        }

        private void FaceTowards(float targetX)
        {
            float diff = targetX - transform.position.x;
            if (Mathf.Abs(diff) > facingDeadZone)
                Context.FacingRight = diff >= 0f;
        }

        private void ApplyFacing()
        {
            transform.localScale = new Vector3(Context.FacingRight ? 1f : -1f, 1f, 1f);
        }

        public void MarkAttackStarted()
        {
            if (Context.Target != null)
                FaceTowards(Context.Target.position.x);
            ApplyFacing();

            IsAttacking = true;
            Context.IsAttacking = true;
            Context.Body.linearVelocity = Vector2.zero;
            attackStartedTime = Time.time;
            attacksSinceLastTeleport++;
        }

        public void MarkAttackEnded()
        {
            IsAttacking = false;
            Context.IsAttacking = false;
            attackCooldownTimer = Mathf.Max(attackCooldown, attackCooldownTimer);
        }

        public bool CanTeleport => canTeleport;
        public bool TeleportToTargetWhenOutOfRange => teleportToTargetWhenOutOfRange;

        public void SetTeleportToTargetWhenOutOfRange(bool enabled)
        {
            teleportToTargetWhenOutOfRange = enabled;
            if (!enabled) outOfRangeTimer = 0f;
        }

        public void MarkTeleportUsed()
        {
            attacksSinceLastTeleport = 0;
            teleportCooldownTimer = teleportMinCooldown;
            outOfRangeTimer = 0f;
            Context.ForceTeleportNearTarget = false;
        }

        private void OnDamaged(DamageInfo info)
        {
            bool isDot = info.DamageType == EDamageType.Dot;
            string crit = info.IsCrit ? " CRIT" : "";
            string roll = info.HasRoll ? $" roll:{info.RollQuality:F2}" : "";
            Debug.Log($"Enemy blud was damaged for {info.Amount} ({info.DamageType}{crit}{roll}). Current Health: {Context.Health.CurrentHealth}");

            AudioManager.PlaySFXAttached(hitEvent, gameObject);

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRed());

            if (isDot) return;

            if (isStaggerImmune)
            {
                PSpawner.Spawn("EnemyHit", transform.position);
                return;
            }

            bool forceAllowsStagger = !Context.IsAttacking
                || Context.CurrentAttackForce != AttackForce.Heavy
                || Context.CurrentAttackForce == AttackForce.Zero;

            if (forceAllowsStagger && staggerImmunityTimer <= 0f)
            {
                animator.SetTrigger("Hurt");
                staggerImmunityTimer = staggerImmunityDuration;
                staggerStunTimer = staggerStunDuration;
                attackCooldownTimer = Mathf.Max(attackCooldownTimer, staggerStunDuration + postStaggerAttackDelay);
                rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
            }

            PSpawner.Spawn("EnemyHit", transform.position);
        }

        private void OnDeath()
        {
            Debug.Log($"Enemy hath died. Rip {name}");

            lootTable?.SpawnInstance(transform.position);
            gameObject.SetActive(false);
        }

        private IEnumerator FlashRed()
        {
            spriteRenderer.color = flashColor;

            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                spriteRenderer.color = Color.Lerp(flashColor, baseColor, t / flashDuration);
                yield return null;
            }

            spriteRenderer.color = baseColor;
            flashRoutine = null;
        }

        public void TriggerLockOn(Transform target)
        {
            Context.Target = target;
            Context.TargetVisible = true;
            Context.TargetPosition = target.position;
            Context.LastKnownTargetPosition = target.position;
            Context.TimeSinceTargetSeen = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (attacks == null || attacks.Count == 0) return;

            float sum = attacks.Sum(a => a.Weight);
            if (Mathf.Abs(sum - 100f) > 0.01f)
                Debug.LogWarning($"{name}: attack weights sum to {sum}, expected 100");
        }
#endif
    }
}