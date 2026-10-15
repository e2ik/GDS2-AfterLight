using System.Collections.Generic;
using System.Collections;
using System.Linq;
using FMODUnity;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

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

        [Header("Terrain Checks")]
        [SerializeField] private bool preventLedgeFalls = true;
        [SerializeField] private bool ignoreTerrainChecks = false;
        [SerializeField] private LayerMask ledgeGroundMask;
        [SerializeField] private float ledgeCheckDistance = 0.5f;
        [SerializeField] private float ledgeCheckDepth = 1f;
        [SerializeField] private float wallCheckDistance = 0.3f;
        [SerializeField] private float wallCheckHeight = 0.1f;

        [Header("Sprite")]
        [SerializeField] private bool spriteFacesLeft = false;

        [Header("Overlap Sorting")]
        [SerializeField] private bool raiseSortingWhenOverlapping = true;
        [SerializeField] private float overlapPadding = 0f;
        private static readonly List<Enemy> sortedEnemies = new List<Enemy>();
        private SortingGroup sortingGroup;
        private int baseSortingOrder;

        [Header("Attack Queue")]
        [SerializeField] private bool queueAttackers = false;
        [SerializeField, Min(0f)] private float queueSpacing = 1.5f;
        [SerializeField, Min(0f)] private float queueYieldTime = 1.5f;
        private float queueYieldUntil;
        private static readonly List<Enemy> queuedEnemies = new List<Enemy>();
        private bool waitingInQueue;

        [Header("Movement Windup")]
        [SerializeField] private bool useMoveWindup = false;
        [SerializeField] private string moveWindupTrigger = "MoveWindup";
        [SerializeField] private string moveWindupState = "MoveWindup";
        [SerializeField] private string moveWinddownTrigger = "MoveWinddown";
        [SerializeField] private string moveWinddownState = "MoveWinddown";
        [SerializeField, Min(0.1f)] private float moveWindupTimeout = 3f;
        [SerializeField, Min(0f)] private float moveStartDelay = 0.15f;
        private float moveWantTimer;
        [SerializeField] private bool windupOnDirectionChange = true;
        [SerializeField] private bool holdAttacksUntilStopped = true;
        [SerializeField] private bool lockTargetOnMoveStart = true;
        [SerializeField, Min(0.05f)] private float lockedArriveDistance = 0.5f;
        private float lockedTargetX;

        [Header("Approach And Retreat")]
        [SerializeField, Range(0f, 1f)] private float approachRetreatChance = 0.3f;
        [SerializeField] private bool approachOnAggro = true;
        [SerializeField] private float approachSpeed = 5f;
        [SerializeField] private float approachStopDistance = 0.5f;
        [SerializeField, Min(0)] private int maxAttacksWithoutApproach = 2;
        [SerializeField, Range(0f, 1f)] private float retreatChance = 0.5f;
        [SerializeField] private bool noBackToBackRetreats = true;
        private int attacksWithoutApproach;
        private bool retreatedLastTime;
        [SerializeField] private float retreatSpeed = 4f;
        [SerializeField] private float retreatDistance = 4f;
        [SerializeField, Min(0.5f)] private float approachRetreatTimeout = 6f;
        [SerializeField] private bool debugApproachRetreat = false;

        private enum ApproachPhase { None, Approach, Retreat }
        private ApproachPhase approachPhase;
        private float approachElapsed;
        private float approachStartTime;
        private float retreatStartX;
        private int retreatDir;

        [Header("Movement Hitbox")]
        [SerializeField] private int moveHitDamage = 10;
        [SerializeField] private AttackForce moveHitForce = AttackForce.Medium;
        [SerializeField] private ParryDirection moveHitParryDirection = ParryDirection.Left;
        private bool moveHitboxArmed;

        private bool moveMoving, moveWindingUp, moveWindingDown, moveHoldsAttacks;
        private bool moveWindupEntered, moveWinddownEntered;
        private float moveWindupElapsed, moveWinddownElapsed;
        private int moveDir;
        private int moveTickFrame = -10;

        [Header("Aggro")]
        [SerializeField] private bool stayStillUntilAggro = false;
        [SerializeField] private Vector2 aggroEffectOffset = Vector2.zero;
        [SerializeField] private bool aggroEffectFollowsEnemy = true;
        private ParticleSystem aggroEffect;
        [SerializeField, Min(0f)] private float aggroAttackDelay = 0.5f;
        private float aggroAttackBlockUntil;
        private bool hasAggroed;
        [SerializeField] private bool canDropAggro = true;
        [SerializeField] private float unreachableGiveUpDelay = 1.5f;
        [SerializeField] private float reaggroCooldown = 3f;
        private float unreachableTimer;
        private float reaggroCooldownTimer;

        [Header("Stagger")]
        [SerializeField] private bool isStaggerImmune = false;
        [SerializeField] private bool staggerOnlyOnComboFinisher = true;
        [SerializeField] private float staggerImmunityDuration = 2f;
        [SerializeField] private float staggerStunDuration = 0.5f;
        [SerializeField] private float postStaggerAttackDelay = 0.5f;
        private float staggerImmunityTimer;
        private float staggerStunTimer;

        public bool IsStaggered => staggerStunTimer > 0f;

        [Header("Hit Reaction")]
        [SerializeField] private bool playHurtOnHit = true;
        [SerializeField] private string hitTrigger = "Hurt";
        private Coroutine hitTriggerResetRoutine;

        [Header("FMOD Events")]
        [SerializeField] private EventReference hitEvent;
        [SerializeField] private EventReference attackEvent;

        [Header("Damage Flash")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField, ColorUsage(false, true)] private Color flashColor = Color.red;
        [SerializeField, ColorUsage(false, true)] private Color blockedFlashColor = Color.white;
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
        [SerializeField, Range(0f, 1f)] private float teleportAfterDamagePercent = 0f;
        [SerializeField, Min(0.5f)] private float teleportInvulnerabilityFailsafe = 6f;
        private int attacksSinceLastTeleport;
        private int damageSinceTeleport;
        private float teleportCooldownTimer;
        private float outOfRangeTimer;

        [Header("Death")] 
        [SerializeField] private bool hasDeathAnimation = false;

        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string deathStateName = "Death";
        [SerializeField] private float deathAnimationTimeout = 5f;

        public EnemyContext Context { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool AttackReady { get; private set; }
        public bool TeleportReady { get; private set; }
        public bool IsBoss => Context != null && Context.BossBounds != null;
        public bool UsesMoveWindup => useMoveWindup;
        public bool IsMoveWindingUp => useMoveWindup && moveWindingUp;
        public bool IsMoveMoving => useMoveWindup && moveMoving;
        public bool IsMoveWindingDown => useMoveWindup && moveWindingDown;

        public bool InMoveWindupPhase => useMoveWindup && Time.frameCount - moveTickFrame <= 2
            && (moveWindingUp || moveMoving || moveWindingDown);

        private float attackCooldownTimer;
        private float attackStartedTime;
        private bool wasTargetingPlayer;

        private void OnEnable()
        {
            Context.Health.OnDamageTaken += OnDamaged;
            Context.Health.OnDeath += OnDeath;
            if (queueAttackers && !queuedEnemies.Contains(this)) queuedEnemies.Add(this);
            if (raiseSortingWhenOverlapping) SetupOverlapSorting();
        }

        private void OnDisable()
        {
            Context.Health.OnDamageTaken -= OnDamaged;
            Context.Health.OnDeath -= OnDeath;
            EnemyCombatTracker.EnemyStoppedTargeting(this);
            if (Context.Health != null) Context.Health.Invulnerable = false;
            queuedEnemies.Remove(this);
            waitingInQueue = false;
            sortedEnemies.Remove(this);
            if (sortingGroup != null) sortingGroup.sortingOrder = baseSortingOrder;

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
                AttackEvents = GetComponentInChildren<AttackEvents>(true),
                FacingRight = true
            };

            Context.AttackStopDistance = attacks.Count > 0 ? attacks.Min(a => a.Attack.Range) : 0.1f;
            Context.HomePosition = transform.position;
            Context.NavPath = new NavMeshPath();
            Context.NoiseSeed = UnityEngine.Random.value * 1000f;
            Context.IgnoreTerrainChecks = ignoreTerrainChecks;
            ApplyTerrainSettings();

            if (spriteRenderer == null)
                Debug.LogError($"{name}: no sprite renderer found");
            else
                baseColor = spriteRenderer.color;

            if (hitFlash == null && spriteRenderer != null)
                hitFlash = spriteRenderer.GetComponent<HitFlash>();

        }

        private void Update()
        {
            Context.IgnoreTerrainChecks = ignoreTerrainChecks;
            ApplyTerrainSettings();

            bool teleportStuck = !float.IsNegativeInfinity(Context.TeleportStartedTime)
                && Time.time - Context.TeleportStartedTime > teleportInvulnerabilityFailsafe
                && (Context.Health == null || Context.Health.CurrentHealth > 0);

            if (teleportStuck && Context.Body != null && !Context.Body.simulated)
            {
                Context.Body.simulated = true;
                if (Context.SpriteRenderer != null) Context.SpriteRenderer.enabled = true;
            }

            if (Context.Health != null && Context.Health.Invulnerable && Time.time - Context.TeleportStartedTime > teleportInvulnerabilityFailsafe)
            {
                Context.Health.Invulnerable = false;
                Context.TeleportStartedTime = float.NegativeInfinity;
            }

            observationSO.Tick(Context, Time.deltaTime);

            if (reaggroCooldownTimer > 0f)
            {
                reaggroCooldownTimer -= Time.deltaTime;
                Context.Target = null;
                Context.TargetVisible = false;
            }

            if (IsBoss && canDropAggro && Context.Target != null && !IsInsideBossBounds(Context.Target.position))
                DropAggro();

            bool isTargetingPlayer = Context.TargetVisible;
            if (isTargetingPlayer != wasTargetingPlayer)
            {
                if(isTargetingPlayer)
                {
                    EnemyCombatTracker.EnemyStartedTargeting(this);
                    hasAggroed = true;
                    if (approachOnAggro) TryStartApproachRetreat(true);
                    // can spawn aggro here
                    aggroEffect = PSpawner.Spawn("Aggro", GetAggroEffectPosition(true));
                    aggroAttackBlockUntil = Time.time + aggroAttackDelay;
                    Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyAggroed);
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

                if (attack.ManualOnly) continue;

                if (attack.InRangeIfUsable) anyAttackInRange = true;

                if (!attack.OnCooldown)
                {
                    allAttacksOnCooldown = false;
                    if (attack.InRangeIfUsable) usableAttackInRange = true;
                }

                if (!IsAttacking && attackCooldownTimer <= 0 && Context.CanReachTarget && attack.IsValid)
                    attackReady = true;
            }

            Context.TargetInRange = usableAttackInRange || (allAttacksOnCooldown && anyAttackInRange);

            UpdateUnreachableGiveUp();
            if (!Context.TargetVisible) attackReady = IsAttacking;
            if (approachPhase != ApproachPhase.None && Time.time - approachStartTime > approachRetreatTimeout + 2f) EndApproachRetreat("never ran (timeout)");
            if (approachPhase != ApproachPhase.None || (moveHoldsAttacks && Time.frameCount - moveTickFrame <= 2)) attackReady = IsAttacking;
            if (Time.time < aggroAttackBlockUntil) attackReady = IsAttacking;
            UpdateAttackQueue();
            if (waitingInQueue) attackReady = IsAttacking;

            AttackReady = attackReady;

            UpdateOutOfRangeTimer();
            bool forceTeleport = IsBoss && teleportToTargetWhenOutOfRange && outOfRangeTeleportDelay > 0f && outOfRangeTimer >= outOfRangeTeleportDelay
                                 && teleportCooldownTimer <= 0f;
            bool damageTeleport = teleportAfterDamagePercent > 0f && Context.Health != null
                                  && damageSinceTeleport >= Context.Health.MaxHealth * teleportAfterDamagePercent;
            bool teleportReady = canTeleport && (forceTeleport || damageTeleport
                || (attacksSinceLastTeleport >= attacksPerTeleport && teleportCooldownTimer <= 0f));
            Context.ForceTeleportNearTarget = canTeleport && forceTeleport && !damageTeleport;
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

            if (!preventLedgeFalls || Context.GroundMask == 0 || Context.IgnoreTerrainChecks) return;

            Vector2 velocity = rb2D.linearVelocity;
            if (Mathf.Abs(velocity.x) < 0.01f) return;
            if (!EnemyTerrainProbe.HasGroundBelow(Context)) return;

            int dir = velocity.x > 0f ? 1 : -1;
            if (!EnemyTerrainProbe.HasGroundAhead(Context, dir))
            {
                rb2D.linearVelocity = new Vector2(0f, velocity.y);
            }
        }

        private void ApplyTerrainSettings()
        {
            if (Context == null) return;

            Context.GroundMask = ledgeGroundMask;
            Context.EdgeCheckDistance = ledgeCheckDistance;
            Context.GroundCheckDepth = ledgeCheckDepth;
            Context.WallCheckDistance = wallCheckDistance;
            Context.WallCheckHeight = wallCheckHeight;
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
            bool isTeleport = module is Enemies.ModuleScripts.Movement.TeleportMovementSO;

            if (IsStaggered && !isTeleport)
            {
                EndApproachRetreat("staggered");
                ResetMoveWindup();
                rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
                return;
            }

            if (stayStillUntilAggro && !hasAggroed && !isTeleport)
            {
                ResetMoveWindup();
                rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
                return;
            }

            if (!useMoveWindup || isTeleport)
            {
                EndApproachRetreat(isTeleport ? "teleport" : "windup off");
                ResetMoveWindup();
                module.Tick(Context, dt);
                if (!isTeleport) ApplyQueueHold();
                return;
            }

            if (Time.frameCount - moveTickFrame > 2) ClearMoveWindup();
            moveTickFrame = Time.frameCount;

            bool facingBefore = Context.FacingRight;
            bool idle = !moveMoving && !moveWindingUp && !moveWindingDown;

            if (idle && Context.Target != null)
                lockedTargetX = Context.Target.position.x;

            if (approachPhase != ApproachPhase.None && (Context.Target == null || IsAttacking))
                EndApproachRetreat(IsAttacking ? "attacking" : "no target");

            if (approachPhase != ApproachPhase.None) TickApproachRetreat(dt);
            else module.Tick(Context, dt);

            if (IsAttacking)
            {
                ResetMoveWindup();
                return;
            }

            float velocityX = rb2D.linearVelocity.x;
            bool wantsMove = Mathf.Abs(velocityX) > 0.01f;

            if (lockTargetOnMoveStart && wantsMove && approachPhase == ApproachPhase.None)
            {
                float lockedDx = lockedTargetX - transform.position.x;

                if (Mathf.Abs(lockedDx) <= lockedArriveDistance)
                {
                    wantsMove = false;
                    rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
                }
                else
                {
                    int lockedDir = lockedDx > 0f ? 1 : -1;
                    rb2D.linearVelocity = new Vector2(lockedDir * Mathf.Abs(velocityX), rb2D.linearVelocity.y);
                    Context.FacingRight = lockedDir > 0;
                }

                velocityX = rb2D.linearVelocity.x;
            }

            int dir = wantsMove ? (velocityX > 0f ? 1 : -1) : moveDir;

            bool canMove = AdvanceMoveWindup(wantsMove, dir, dt);
            UpdateMoveHitbox();
            moveHoldsAttacks = holdAttacksUntilStopped && (moveMoving || moveWindingDown);

            if (canMove)
            {
                ApplyQueueHold();
                return;
            }

            rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
            if (moveWindingDown) Context.FacingRight = facingBefore;
        }

        private void UpdateAttackQueue()
        {
            waitingInQueue = false;
            if (!queueAttackers || IsAttacking || Context.Target == null || !Context.TargetVisible) return;

            float targetX = Context.Target.position.x;
            float myOffset = transform.position.x - targetX;
            float myDistance = Mathf.Abs(myOffset);
            float closest = float.MaxValue;
            Enemy leader = null;

            foreach (Enemy other in queuedEnemies)
            {
                if (other == null || !other.isActiveAndEnabled || other.Context == null) continue;
                if (other.Context.Target != Context.Target || !other.Context.TargetVisible) continue;
                if (other.Context.Health != null && other.Context.Health.CurrentHealth <= 0) continue;
                if (!other.IsAttacking && Time.time < other.queueYieldUntil) continue;

                float offset = other.transform.position.x - targetX;
                if (Mathf.Sign(offset) != Mathf.Sign(myOffset)) continue;

                float distance = Mathf.Abs(offset);
                if (other.IsAttacking) distance = -1f;

                if (distance < closest)
                {
                    closest = distance;
                    leader = other;
                }
            }

            if (leader == null || leader == this) return;

            waitingInQueue = true;
            queueHoldDistance = Mathf.Max(0f, Mathf.Abs(leader.transform.position.x - targetX)) + queueSpacing;
        }

        private float queueHoldDistance;

        private void ApplyQueueHold()
        {
            if (!waitingInQueue || Context.Target == null) return;

            float toTarget = Context.Target.position.x - transform.position.x;
            bool movingToward = Mathf.Abs(rb2D.linearVelocity.x) > 0.01f && Mathf.Sign(rb2D.linearVelocity.x) == Mathf.Sign(toTarget);

            if (movingToward && Mathf.Abs(toTarget) <= queueHoldDistance)
                rb2D.linearVelocity = new Vector2(0f, rb2D.linearVelocity.y);
        }

        private bool AdvanceMoveWindup(bool wantsMove, int dir, float dt)
        {
            if (moveWindingDown)
            {
                moveWinddownElapsed += dt;
                if (!MovePhaseFinished(moveWinddownState, ref moveWinddownEntered, moveWinddownElapsed)) return false;

                moveWindingDown = false;
            }

            if (!wantsMove)
            {
                moveWantTimer = 0f;

                if (moveWindingUp)
                {
                    SetMoveTrigger(moveWindupTrigger, false);
                    moveWindingUp = false;
                }

                if (moveMoving) StartMoveWinddown();
                return false;
            }

            if (moveMoving && windupOnDirectionChange && dir != moveDir)
            {
                StartMoveWinddown();
                if (moveWindingDown) return false;
            }

            if (!moveMoving)
            {
                if (!moveWindingUp)
                {
                    moveWantTimer += dt;
                    if (moveWantTimer < moveStartDelay) return false;
                    moveWantTimer = 0f;

                    if (!HasMoveTrigger(moveWindupTrigger))
                    {
                        moveMoving = true;
                        moveDir = dir;
                        return true;
                    }

                    moveWindingUp = true;
                    moveWindupEntered = false;
                    moveWindupElapsed = 0f;
                    SetMoveTrigger(moveWindupTrigger, true);
                }

                moveWindupElapsed += dt;
                if (!MovePhaseFinished(moveWindupState, ref moveWindupEntered, moveWindupElapsed)) return false;

                moveWindingUp = false;
                moveMoving = true;
            }

            moveDir = dir;
            return true;
        }

        private void StartMoveWinddown()
        {
            moveMoving = false;
            if (!HasMoveTrigger(moveWinddownTrigger)) return;

            moveWindingDown = true;
            moveWinddownEntered = false;
            moveWinddownElapsed = 0f;
            SetMoveTrigger(moveWinddownTrigger, true);
        }

        private bool MovePhaseFinished(string stateName, ref bool entered, float elapsed)
        {
            if (elapsed >= moveWindupTimeout) return true;

            Animator animator = Context.Animator;
            if (animator == null || string.IsNullOrEmpty(stateName)) return true;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(stateName))
            {
                entered = true;
                return state.normalizedTime >= 1f;
            }

            return entered;
        }

        private void ResetMoveWindup()
        {
            if (moveWindingUp) SetMoveTrigger(moveWindupTrigger, false);
            if (moveWindingDown) SetMoveTrigger(moveWinddownTrigger, false);

            ClearMoveWindup();
        }

        private void TryStartApproachRetreat(bool guaranteed = false)
        {
            if (!useMoveWindup || Context.Target == null || IsAttacking)
            {
                LogApproach($"not started (useMoveWindup {useMoveWindup}, has target {Context.Target != null}, attacking {IsAttacking})");
                return;
            }
            if (Context.Health != null && Context.Health.CurrentHealth <= 0) return;
            bool forced = !guaranteed && maxAttacksWithoutApproach > 0 && attacksWithoutApproach >= maxAttacksWithoutApproach;

            if (!guaranteed && !forced && (approachRetreatChance <= 0f || UnityEngine.Random.value >= approachRetreatChance))
            {
                attacksWithoutApproach++;
                LogApproach($"roll failed ({attacksWithoutApproach} attacks without approach)");
                return;
            }

            attacksWithoutApproach = 0;
            if (forced) LogApproach("forced after too many attacks in a row");

            LogApproach(guaranteed ? "started (aggro)" : "started (roll)");

            approachPhase = ApproachPhase.Approach;
            approachElapsed = 0f;
            approachStartTime = Time.time;
        }

        private void LogApproach(string message)
        {
            if (debugApproachRetreat) Debug.Log($"[ApproachRetreat] {name}: {message}", this);
        }

        private void EndApproachRetreat(string reason = "finished")
        {
            if (approachPhase != ApproachPhase.None) LogApproach($"ended during {approachPhase}: {reason}");
            approachPhase = ApproachPhase.None;
            approachElapsed = 0f;
        }

        private void TickApproachRetreat(float dt)
        {
            approachElapsed += dt;

            float selfX = transform.position.x;
            float targetX = lockTargetOnMoveStart ? lockedTargetX : Context.Target.position.x;
            float dx = targetX - selfX;
            int toTarget = dx >= 0f ? 1 : -1;
            bool timedOut = approachElapsed >= approachRetreatTimeout;
            float velocityX = 0f;

            Context.FacingRight = toTarget > 0;

            if (approachPhase == ApproachPhase.Approach)
            {
                bool reached = Mathf.Abs(dx) <= approachStopDistance;
                bool blocked = !Context.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(Context, toTarget);

                if (reached || blocked || timedOut)
                {
                    LogApproach($"approach done (reached {reached}, blocked {blocked}, timed out {timedOut})");

                    bool blockedByLast = noBackToBackRetreats && retreatedLastTime;

                    if (blockedByLast || UnityEngine.Random.value >= retreatChance)
                    {
                        retreatedLastTime = false;
                        EndApproachRetreat(blockedByLast ? "retreated last time" : "retreat roll failed");
                    }
                    else
                    {
                        retreatedLastTime = true;
                        approachPhase = ApproachPhase.Retreat;
                        retreatDir = -toTarget;
                        retreatStartX = selfX;
                    }
                }
                else
                {
                    velocityX = toTarget * approachSpeed;
                }
            }
            else
            {
                bool travelled = Mathf.Abs(selfX - retreatStartX) >= retreatDistance;
                bool blocked = !Context.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(Context, retreatDir);

                if (travelled || blocked || timedOut)
                {
                    LogApproach($"retreat done (travelled {travelled}, blocked {blocked}, timed out {timedOut})");
                    EndApproachRetreat();
                }
                else velocityX = retreatDir * retreatSpeed;
            }

            rb2D.linearVelocity = new Vector2(velocityX, rb2D.linearVelocity.y);
        }

        private void UpdateMoveHitbox()
        {
            bool inPhase = moveWindingUp || moveMoving || moveWindingDown;
            if (inPhase && !moveHitboxArmed) ArmMoveHitbox();
            else if (!inPhase) DisarmMoveHitbox();
        }

        private void ArmMoveHitbox()
        {
            moveHitboxArmed = true;

            AttackEvents events = Context.AttackEvents;
            if (events == null) return;

            events.CurrentDamage = moveHitDamage;
            events.CurrentAttackForce = moveHitForce;
            events.CurrentParryDirection = moveHitParryDirection;
        }

        public bool TryGetMoveHitValues(out int damage, out AttackForce force, out ParryDirection direction)
        {
            damage = moveHitDamage;
            force = moveHitForce;
            direction = moveHitParryDirection;
            return !IsAttacking && InMoveWindupPhase;
        }

        private void DisarmMoveHitbox()
        {
            if (!moveHitboxArmed) return;

            moveHitboxArmed = false;
            Context.AttackEvents?.DisableHitbox();
        }

        private void ClearMoveWindup()
        {
            DisarmMoveHitbox();
            moveMoving = false;
            moveWindingUp = false;
            moveWindingDown = false;
            moveHoldsAttacks = false;
            moveWindupEntered = false;
            moveWinddownEntered = false;
            moveWindupElapsed = 0f;
            moveWinddownElapsed = 0f;
            moveWantTimer = 0f;
        }

        private void SetMoveTrigger(string trigger, bool set)
        {
            if (!HasMoveTrigger(trigger)) return;

            if (set) Context.Animator.SetTrigger(trigger);
            else Context.Animator.ResetTrigger(trigger);
        }

        private bool HasMoveTrigger(string trigger)
        {
            Animator animator = Context.Animator;
            if (animator == null || string.IsNullOrEmpty(trigger)) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger) return true;

            return false;
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
            if (approachPhase != ApproachPhase.None || (moveHoldsAttacks && Time.frameCount - moveTickFrame <= 2)) return false;
            if (Time.time < aggroAttackBlockUntil) return false;
            if (waitingInQueue) return false;

            float totalWeight = attacks.Where(a => a.IsValid && !a.ManualOnly).Sum(a => a.Weight);
            if (totalWeight <= 0f) return false;

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (AttackInstance attack in attacks)
            {
                if (!attack.IsValid || attack.ManualOnly) continue;
                cumulative += attack.Weight;

                if (roll <= cumulative)
                {
                    selected = attack;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetAttackInstance(EnemyAttackSO attackSO, out AttackInstance instance)
        {
            instance = null;
            if (IsAttacking || attackSO == null)
                return false;

            instance = attacks.FirstOrDefault(a => a.Attack == attackSO);
            return instance != null;
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
            EndApproachRetreat("dropped aggro");
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
            bool flip = Context.FacingRight == spriteFacesLeft;
            transform.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        }

        private Vector3 GetAggroEffectPosition(bool faceTarget = false)
        {
            bool faceRight = transform.lossyScale.x >= 0f;

            if (faceTarget && Context != null && Context.Target != null)
                faceRight = Context.Target.position.x >= transform.position.x;

            float x = Mathf.Abs(transform.lossyScale.x) * aggroEffectOffset.x * (faceRight ? 1f : -1f);
            float y = transform.lossyScale.y * aggroEffectOffset.y;
            return transform.position + new Vector3(x, y, 0f);
        }

        private void LateUpdate()
        {
            UpdateOverlapSorting();
            UpdateAggroEffectFollow();
        }

        private void UpdateAggroEffectFollow()
        {
            if (aggroEffect == null) return;

            if (!aggroEffectFollowsEnemy || !aggroEffect.gameObject.activeInHierarchy || Time.time >= aggroAttackBlockUntil + 1f)
            {
                aggroEffect = null;
                return;
            }

            aggroEffect.transform.position = GetAggroEffectPosition();
        }

        private void SetupOverlapSorting()
        {
            if (spriteRenderer == null) return;

            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
                if (sortingGroup == null)
                {
                    sortingGroup = gameObject.AddComponent<SortingGroup>();
                    sortingGroup.sortingLayerID = spriteRenderer.sortingLayerID;
                    sortingGroup.sortingOrder = spriteRenderer.sortingOrder;
                }
                baseSortingOrder = sortingGroup.sortingOrder;
            }

            if (!sortedEnemies.Contains(this)) sortedEnemies.Add(this);
        }

        private void UpdateOverlapSorting()
        {
            if (!raiseSortingWhenOverlapping || sortingGroup == null || spriteRenderer == null) return;

            Bounds bounds = spriteRenderer.bounds;
            bounds.Expand(overlapPadding * 2f);
            int rank = 0;

            for (int i = 0; i < sortedEnemies.Count; i++)
            {
                Enemy other = sortedEnemies[i];
                if (other == null || other == this || other.spriteRenderer == null || other.sortingGroup == null) continue;
                if (other.sortingGroup.sortingLayerID != sortingGroup.sortingLayerID) continue;
                if (other.baseSortingOrder != baseSortingOrder) continue;
                if (!other.spriteRenderer.enabled || !bounds.Intersects(other.spriteRenderer.bounds)) continue;
                if (WinsSortingAgainst(other)) rank++;
            }

            int order = baseSortingOrder + rank;
            if (sortingGroup.sortingOrder != order) sortingGroup.sortingOrder = order;
        }

        private bool WinsSortingAgainst(Enemy other)
        {
            if (IsAttacking != other.IsAttacking) return IsAttacking;
            return GetInstanceID() > other.GetInstanceID();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 point = GetAggroEffectPosition();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(point, 0.15f);
            Gizmos.DrawLine(point + Vector3.left * 0.25f, point + Vector3.right * 0.25f);
            Gizmos.DrawLine(point + Vector3.down * 0.25f, point + Vector3.up * 0.25f);
        }

        public void MarkAttackStarted()
        {
            EndApproachRetreat("attack started");
            Context.AttackEvents?.CloseBonusWindow();

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
            bool wasAttacking = IsAttacking;
            IsAttacking = false;
            if (wasAttacking && queueAttackers) queueYieldUntil = Time.time + queueYieldTime;
            Context.AttackEvents?.CloseBonusWindow();
            Context.IsAttacking = false;
            attackCooldownTimer = Mathf.Max(attackCooldown, attackCooldownTimer);

            if (wasAttacking) TryStartApproachRetreat();
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
            damageSinceTeleport = 0;
            teleportCooldownTimer = teleportMinCooldown;
            outOfRangeTimer = 0f;
            Context.ForceTeleportNearTarget = false;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (canTeleport && IsBoss) damageSinceTeleport += info.Amount;

            bool isDot = info.DamageType == EDamageType.Dot;
            string crit = info.IsCrit ? " CRIT" : "";
            string roll = info.HasRoll ? $" roll:{info.RollQuality:F2}" : "";
            Debug.Log($"Enemy blud was damaged for {info.Amount} ({info.DamageType}{crit}{roll}). Current Health: {Context.Health.CurrentHealth}");

            AudioManager.PlaySFXAttached(hitEvent, gameObject);

            if (hitFlash != null)
            {
                if (info.Blocked > 0) hitFlash.Flash(blockedFlashColor, HitFlash.FlashMode.Edge);
                else hitFlash.Flash(flashColor);
            }
            else
            {
                if (flashRoutine != null)
                    StopCoroutine(flashRoutine);
                flashRoutine = StartCoroutine(FlashRed());
            }

            if (isDot) return;

            if (Context.Health.CurrentHealth <= 0)
            {
                PSpawner.Spawn("EnemyHit", transform.position);
                return;
            }

            if (isStaggerImmune || (staggerOnlyOnComboFinisher && !info.IsComboFinisher))
            {
                PlayHitReaction();
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
            else
            {
                PlayHitReaction();
            }

            PSpawner.Spawn("EnemyHit", transform.position);
        }

        private void PlayHitReaction()
        {
            if (!playHurtOnHit || animator == null || Context.IsAttacking || IsStaggered) return;
            animator.SetTrigger(hitTrigger);

            if (hitTriggerResetRoutine != null) StopCoroutine(hitTriggerResetRoutine);
            hitTriggerResetRoutine = StartCoroutine(ResetHitTriggerSoon());
        }

        private IEnumerator ResetHitTriggerSoon()
        {
            yield return null;
            yield return null;
            if (animator != null) animator.ResetTrigger(hitTrigger);
            hitTriggerResetRoutine = null;
        }

        private void OnDeath()
        {
            Debug.Log($"Enemy hath died. Rip {name}");

            lootTable?.SpawnInstance(transform.position);
            
            FreezeForDeathSequence();

            if (hasDeathAnimation && animator != null)
            {
                animator.ResetTrigger(hitTrigger);
                animator.ResetTrigger("Hurt");

                int deathHash = Animator.StringToHash(deathStateName);
                if (animator.HasState(0, deathHash))
                    animator.Play(deathHash, 0, 0f);
                else
                    animator.SetTrigger(deathTrigger);

                StartCoroutine(PlayDeathAnimationThenDeactivate());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        
        private IEnumerator PlayDeathAnimationThenDeactivate()
        {
            yield return null;

            float duration = GetDeathClipDuration();
            float elapsed = Time.deltaTime;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            gameObject.SetActive(false);
        }

        private float GetDeathClipDuration()
        {
            AnimatorStateInfo state = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);

            if (state.IsName(deathStateName) && state.length > 0f && !float.IsInfinity(state.length))
                return Mathf.Min(state.length, deathAnimationTimeout);

            AnimatorClipInfo[] clips = animator.IsInTransition(0)
                ? animator.GetNextAnimatorClipInfo(0)
                : animator.GetCurrentAnimatorClipInfo(0);

            if (clips.Length > 0 && clips[0].clip != null)
            {
                float speed = Mathf.Abs(animator.speed * state.speed);
                float length = speed > 0.0001f ? clips[0].clip.length / speed : clips[0].clip.length;
                return Mathf.Min(length, deathAnimationTimeout);
            }

            return deathAnimationTimeout;
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

        public void FreezeForDeathSequence()
        {
            if (behaviorAgent != null)
                behaviorAgent.enabled = false;

            rb2D.linearVelocity = Vector2.zero;
            IsAttacking = false;
            Context.IsAttacking = false;

            foreach (HitBox hitBox in GetComponentsInChildren<HitBox>())
                hitBox.Disable();

            AttackEvents events = Context.AttackEvents != null ? Context.AttackEvents : GetComponentInChildren<AttackEvents>();
            if (events != null) events.CloseParryWindow();
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