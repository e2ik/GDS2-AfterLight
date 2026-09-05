using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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

        [SerializeField] private BehaviorGraphAgent behaviorAgent;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D rb2D;
        [SerializeField] private float attackCooldown;
        [SerializeField] private string placeholderClipName = "EmptyAttack";

        public EnemyContext Context { get; private set; }
        public bool IsAttacking { get; private set; }

        private float attackCooldownTimer;

        private void OnEnable()
        {
            Context.Health.OnDamaged += OnDamaged;
            Context.Health.OnDeath += OnDeath;
        }

        private void OnDisable()
        {
            Context.Health.OnDamaged -= OnDamaged;
            Context.Health.OnDeath -= OnDeath;
        }


        private void Awake()
        {
            var overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;

            var overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overridesList);
            AnimationClip placeholderClip =
                overridesList.FirstOrDefault(pair => pair.Key.name == placeholderClipName).Key;
            
            if(placeholderClip == null)
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

        }

        private void Update()
        {
            observationSO.Tick(Context, Time.deltaTime);

            if (!IsAttacking)
                transform.localScale = new Vector3(Context.FacingRight ? 1f : -1f, 1f, 1f);

            animator.SetFloat("Speed", Mathf.Abs(Context.Body.linearVelocityX));

            attackCooldownTimer = Mathf.Max(0, attackCooldownTimer - Time.deltaTime);
            
            bool attackReady = false;

           
            foreach (AttackInstance attack in attacks)
            {
                attack.Tick(Context, Time.deltaTime);
                if (attackCooldownTimer <= 0 && !IsAttacking && Context.CanReachTarget && attack.IsValid)
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
        }

        public void MarkAttackEnded()
        {
            IsAttacking = false;
            Context.IsAttacking = false;
            attackCooldownTimer = attackCooldown;
        }

        private void OnDamaged(int amount, int currentHealth)
        {
            Debug.Log($"Enemy blud was damaged for {amount}. Current Health: {currentHealth}");

            // if we're able to get the attack type e.g. zero, light medium
            // on < light we trigger anim (this acts as an interrupt with no other code changes)
            // since attack is decided by the animation

            //Debug.Log("Current Attack Force " + Context.CurrentAttackForce + " Is Attacking: " + Context.IsAttacking);
            
            if (!Context.IsAttacking || (Context.CurrentAttackForce == AttackForce.Light) || Context.CurrentAttackForce == AttackForce.Zero) 
                animator.SetTrigger("Hurt");
                
            
        }

        private void OnDeath()
        {
            Debug.Log($"Enemy hath died. Rip {name}");
            gameObject.SetActive(false);
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (attacks == null || attacks.Count == 0) return;

            float sum = attacks.Sum(a => a.Weight);
            if(Mathf.Abs(sum - 100f) > 0.01f)
                Debug.LogWarning($"{name}: attack weights sum to {sum}, expected 100");
        }
#endif
    }
}
