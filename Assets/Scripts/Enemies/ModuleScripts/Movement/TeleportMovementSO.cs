using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    public enum TeleportPhase {TeleportingOut, Hidden, TeleportingIn, Finished}
    
    [CreateAssetMenu(menuName = "Enemies/Movement/Teleport")]
    public class TeleportMovementSO : EnemyMovementSO
    {
        [Header("Destination")] 
        [SerializeField] private bool changeY = false;
        [SerializeField] private float minDistanceFromTarget = 3f;
        [SerializeField] private float navMeshSampleDistance = 1f;
        [SerializeField] private int maxRerollAttempts = 20;

        [Header("Animation")] 
        [SerializeField] private string teleportOutTrigger = "TeleportOut";
        [SerializeField] private string teleportInTrigger = "TeleportIn";
        [SerializeField] private string teleportOutStateName = "TeleportOut";
        [SerializeField] private string teleportInStateName = "TeleportIn";
        [SerializeField] private float hiddenDuration = 1.5f;
        

        public void Begin(EnemyContext ctx)
        {
            ctx.Body.linearVelocity = Vector2.zero;
            ctx.TeleportDestination = PickDestination(ctx);
            
            ctx.Animator.SetTrigger(teleportOutTrigger);
            ctx.TeleportPhase = TeleportPhase.TeleportingOut;
        }
        
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            ctx.Body.linearVelocity = Vector2.zero;
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);

            switch (ctx.TeleportPhase)
            {
                case TeleportPhase.TeleportingOut:
                    if (state.IsName(teleportOutStateName) && state.normalizedTime >= 1f)
                    {
                        SetHidden(ctx, true);
                        ctx.Self.position = ctx.TeleportDestination;
                        ctx.TeleportPhase = TeleportPhase.Hidden;
                        ctx.TeleportTimer = hiddenDuration;
                    }
                    break;
                
                case TeleportPhase.Hidden:
                    ctx.TeleportTimer -= deltaTime;
                    if (ctx.TeleportTimer <= 0f)
                    {
                        SetHidden(ctx, false);
                        ctx.Animator.SetTrigger(teleportInTrigger);
                        ctx.TeleportPhase = TeleportPhase.TeleportingIn;
                    }
                    break;
                
                case TeleportPhase.TeleportingIn:
                    if (state.IsName(teleportInStateName) && state.normalizedTime >= 0.9f)
                        ctx.TeleportPhase = TeleportPhase.Finished;
                    break;
            }
        }

        public bool IsFinished(EnemyContext ctx) => ctx.TeleportPhase == TeleportPhase.Finished;

        private void SetHidden(EnemyContext ctx, bool hidden)
        {
            if (ctx.SpriteRenderer != null)
                ctx.SpriteRenderer.enabled = !hidden;

            ctx.Body.simulated = !hidden;
        }

        private Vector2 PickDestination(EnemyContext ctx)
        {
            if (ctx.BossBounds == null)
            {
                Debug.LogWarning($"{name}: no BossBounds assigned on this enemies context");
                return ctx.Self.position;
            }

            Bounds bounds = ctx.BossBounds.WorldBounds;
            bool hasTarget = ctx.Target != null;
            Vector2 targetPos = ctx.TargetPosition;
            float minDistSqr = minDistanceFromTarget * minDistanceFromTarget;

            for (int attempt = 0; attempt < maxRerollAttempts; attempt++)
            {
                float x = Random.Range(bounds.min.x, bounds.max.x);
                float y = changeY ? Random.Range(bounds.min.y, bounds.max.y) : ctx.Self.position.y;
                Vector2 candidate = new Vector2(x, y);

                if (hasTarget && (candidate - targetPos).sqrMagnitude < minDistSqr)
                    continue;

                if (!NavMeshFlightUtility.TrySamplePoint(candidate, navMeshSampleDistance, out Vector2 navPoint))
                    continue;

                if (hasTarget && (navPoint - targetPos).sqrMagnitude < minDistSqr)
                    continue;

                return navPoint;
            }
            
            Debug.LogWarning($"{name}: couldn't find a valid teleport destination after {maxRerollAttempts} attempts");
            return ctx.Self.position;
        }
    }
}