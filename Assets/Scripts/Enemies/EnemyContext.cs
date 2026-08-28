using Unity.Behavior;
using UnityEngine;

namespace Enemies
{
    public class EnemyContext
    {
        public Transform Self;
        public Rigidbody2D Body;
        public EnemyHealth Health;
        public BehaviorGraphAgent Behavior;
        public Animator Animator;
        public AnimatorOverrideController OverrideController;

        public bool FacingRight;
        public bool IsAttacking;
        public float AttackStopDistance;

        public Transform Target;
        public bool TargetVisible;
        public bool TargetInRange;
        public Vector2 TargetPosition;
    }
}
