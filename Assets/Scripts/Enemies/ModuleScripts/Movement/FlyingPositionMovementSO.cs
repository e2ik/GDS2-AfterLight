using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Flying Position")]
    public class FlyingPositionMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private Vector2 hoverPosition = new Vector2(2, 3);
        [SerializeField] private float arriveDistance = 0.3f;
        [SerializeField] private float cornerReachDistance = 0.2f;
        [SerializeField] private float repathInterval = 0.5f;
        
        [Header("Wobble")] 
        [SerializeField] private float wobbleAmplitude = 0.5f;
        [SerializeField] private float wobbleFrequency = 1f;
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking || ctx.Target == null) return;

            ctx.RepathTimer -= deltaTime;
            if (ctx.RepathTimer <= 0f)
            {
                Vector2 desired = ctx.TargetPosition + ((ctx.TargetPosition.x <= ctx.Self.position.x) ? hoverPosition : new Vector2(hoverPosition.x * -1, hoverPosition.y));
                ctx.CanReachTarget = NavMeshFlightUtility.TryCalculatePath(ctx.Self.position, desired, ctx.NavPath);
                ctx.PathCornerIndex = 0;
                ctx.RepathTimer = repathInterval;
            }

            if (!ctx.CanReachTarget)
            {
                ctx.Body.linearVelocity = Vector2.zero;
                return;
            }

            bool arrived = ctx.NavPath.corners.Length > 0 && ctx.PathCornerIndex >= ctx.NavPath.corners.Length - 1 &&
                           Vector2.Distance(ctx.Self.position, ctx.NavPath.corners[^1]) <= arriveDistance;

            if (arrived)
            {
                ctx.Body.linearVelocity = GetWobble(ctx);
                return;
            }

            Vector2 dir = NavMeshFlightUtility.GetSteeringDirection(ctx.NavPath, ref ctx.PathCornerIndex,
                ctx.Self.position, cornerReachDistance);

            if (Mathf.Abs(dir.x) > 0.01f)
                ctx.FacingRight = dir.x >= 0f;

            ctx.Body.linearVelocity = dir * moveSpeed + GetWobble(ctx);
        }
        
        private Vector2 GetWobble(EnemyContext ctx)
        {
            float t = Time.time * wobbleFrequency;
            float noiseX = Mathf.PerlinNoise(ctx.NoiseSeed, t) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(ctx.NoiseSeed + 100f, t) * 2f - 1f;
            return new Vector2(noiseX, noiseY) * wobbleAmplitude;
        }
    }
}
