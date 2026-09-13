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
        [SerializeField] private string placeholderClipName = "EmptyAttack";

        [Header("Stagger")]
        [SerializeField] private bool isStaggerImmune = false;
        [SerializeField] private float staggerImmunityDuration = 2f;
        private float staggerImmunityTimer;

        [Header("FMOD Events")] 
        [SerializeField] private EventReference hitEvent;
    
        [Header("Damage Flash")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField] private float flashDuration = 0.15f;
        private Coroutine flashRoutine;
        private Color baseColor;

        public EnemyContext Context { get; private set; }
        public bool IsAttacking { get; private set; }

        private float attackCooldownTimer;
        private bool wasTargetingPlayer;

        private void OnEnable()
        {
            Context.Health.OnDamaged += OnDamaged;
            Context.Health.OnDeath += OnDeath;
        }

        private void OnDisable()
        {
            Context.Health.OnDamaged -= OnDamaged;
            Context.Health.OnDeath -= OnDeath;
            EnemyCombatTracker.EnemyStoppedTargeting(this);
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
            observationSO.Tick(Context, Time.deltaTime);

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

            if (!IsAttacking)
                transform.localScale = new Vector3(Context.FacingRight ? 1f : -1f, 1f, 1f);

            animator.SetFloat("Speed", Mathf.Abs(Context.Body.linearVelocityX));

            attackCooldownTimer = Mathf.Max(0, attackCooldownTimer - Time.deltaTime);
            staggerImmunityTimer = Mathf.Max(0, staggerImmunityTimer - Time.deltaTime);

            bool attackReady = IsAttacking;

            foreach (AttackInstance attack in attacks)
            {
                attack.Tick(Context, Time.deltaTime);
                if (!IsAttacking && attackCooldownTimer <= 0 && Context.CanReachTarget && attack.IsValid)
                    attackReady = true;
            }

            behaviorAgent.BlackboardReference.SetVariableValue("TargetVisible", Context.TargetVisible);
            behaviorAgent.BlackboardReference.SetVariableValue("TargetPosition", Context.TargetPosition);
            behaviorAgent.BlackboardReference.SetVariableValue("AttackReady", attackReady);
            behaviorAgent.BlackboardReference.SetVariableValue("Self", gameObject);
        }

        public void RunMovement(EnemyMovementSO module, float dt) => module.Tick(Context, dt);

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

        public void MarkAttackStarted()
        {
            IsAttacking = true;
            Context.IsAttacking = true;
            Context.Body.linearVelocity = Vector2.zero;
        }

        public void MarkAttackEnded()
        {
            IsAttacking = false;
            Context.IsAttacking = false;
            attackCooldownTimer = attackCooldown;
        }

        private void OnDamaged(int amount, int currentHealth, bool isDot)
        {
            Debug.Log($"Enemy blud was damaged for {amount}. Current Health: {currentHealth}");

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