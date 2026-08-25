using System;
using Unity.Behavior;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(EnemyHealth))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private EnemyObservationSO observationSO;
        [SerializeField] private EnemyMovementSO movementSO;
        [SerializeField] private EnemyAttackSO attackSO;
        [SerializeField] private float attackRange = 1.2f;
        
        [SerializeField] private BehaviorGraphAgent behaviorAgent;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D rb2D;

        public EnemyContext Context { get; private set; }

        private void Awake()
        {
            var overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;
            
            Context = new EnemyContext()
            {
                Self = transform,
                Body = rb2D,
                Health = GetComponent<EnemyHealth>(),
                Behavior = behaviorAgent,
                Animator = animator,
                OverrideController = overrideController,
                FacingRight = true
            };
        }

        private void Update()
        {
            observationSO.Tick(Context, Time.deltaTime);
            
            transform.localScale = new Vector3(Context.FacingRight ? 1f : -1f, 1f, 1f);
            animator.SetFloat("Speed", Mathf.Abs(Context.Body.linearVelocityX));
            
            bool inRange = Context.TargetVisible && Vector2.Distance(transform.position, Context.TargetPosition) <= attackRange;
            
            behaviorAgent.BlackboardReference.SetVariableValue("TargetVisible", Context.TargetVisible);
            behaviorAgent.BlackboardReference.SetVariableValue("TargetPosition", Context.TargetPosition);
            behaviorAgent.BlackboardReference.SetVariableValue("InAttackRange", inRange);
            behaviorAgent.BlackboardReference.SetVariableValue("Self", gameObject);
        }
        
        public void RunMovement(EnemyMovementSO module, float dt) => module.Tick(Context, dt);
        public void RunAttack(EnemyAttackSO module, float dt) => module.Tick(Context, dt);
    }
}
