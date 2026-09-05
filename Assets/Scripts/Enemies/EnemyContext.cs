using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

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
        public AnimationClip PlaceholderClip;

        public Vector3 HomePosition;
        public NavMeshPath NavPath;
        public int PathCornerIndex;
        public bool CanReachTarget = true;
        public float PatrolWaitTimer;
        public float RepathTimer;
        public float NoiseSeed;
        public bool reachedTarget;

        public Transform Target;
        public bool TargetVisible;
        public bool TargetInRange;
        public Vector2 TargetPosition;
    }
}
