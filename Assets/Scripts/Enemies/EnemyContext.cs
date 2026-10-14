using Enemies.ModuleScripts.Movement;
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
        public AttackForce CurrentAttackForce;
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
        public float TimeSinceTargetSeen;
        public Vector2 LastKnownTargetPosition;
        public float AttackElapsed;
        public float LastChaseBlockedTime = float.NegativeInfinity;

        public MoveWindupState MoveWindup = new MoveWindupState();
        public ApproachRetreatState ApproachRetreat = new ApproachRetreatState();
        public int ApproachCyclesSinceAttack;
        public bool ApproachCyclesSatisfied;
        
        public bool IgnoreTerrainChecks;
        public LayerMask GroundMask;
        public float EdgeCheckDistance = 0.5f;
        public float GroundCheckDepth = 1f;
        public float WallCheckDistance = 0.3f;
        public float WallCheckHeight = 0.1f;

        public AttackEvents AttackEvents;
        public int ComboStepIndex;
        public bool ComboNextArmed;
        public bool ComboStopped;
        public int ComboTelegraphFrame = -10;

        public BossBounds BossBounds;
        public TeleportPhase TeleportPhase;
        public float TeleportTimer;
        public float TeleportStartedTime = float.NegativeInfinity;
        public Vector2 TeleportDestination;
        public bool ForceTeleportNearTarget;
        public SpriteRenderer SpriteRenderer;
    }
}