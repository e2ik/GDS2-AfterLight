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

        [Header("Settling")]
        [SerializeField] private bool ignoreTargetJumps = true;
        [SerializeField, Min(0f)] private float repositionThreshold = 1.5f;
        
        [Header("Separation")]
        [SerializeField, Min(0f)] private float separationRadius = 1.2f;
        [SerializeField, Min(0f)] private float separationStrength = 3f;
        [SerializeField] private LayerMask separationMask;

        [Header("Wobble")] 
        [SerializeField] private float wobbleAmplitude = 0.5f;
        [SerializeField] private float wobbleFrequency = 1f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking || ctx.Target == null) return;

            ctx.RepathTimer -= deltaTime;
            if (ctx.RepathTimer <= 0f)
            {
                ctx.RepathTimer = repathInterval;

                Vector2 desired = GetDesiredPosition(ctx);
                bool needsMove = !ctx.HasHoverDestination || Vector2.Distance(desired, ctx.HoverDestination) > repositionThreshold;

                if (needsMove)
                {
                    ctx.HoverDestination = desired;
                    ctx.HasHoverDestination = true;
                    ctx.CanReachTarget = NavMeshFlightUtility.TryCalculatePath(ctx.Self.position, desired, ctx.NavPath);
                    ctx.PathCornerIndex = 0;
                }
            }

            if (!ctx.CanReachTarget)
            {
                ctx.Body.linearVelocity = Separation(ctx);
                return;
            }

            bool arrived = ctx.NavPath.corners.Length > 0 && ctx.PathCornerIndex >= ctx.NavPath.corners.Length - 1 &&
                           Vector2.Distance(ctx.Self.position, ctx.NavPath.corners[^1]) <= arriveDistance;

            if (arrived)
            {
                FaceTarget(ctx);
                ctx.Body.linearVelocity = GetWobble(ctx) + Separation(ctx);
                return;
            }

            Vector2 dir = NavMeshFlightUtility.GetSteeringDirection(ctx.NavPath, ref ctx.PathCornerIndex,
                ctx.Self.position, cornerReachDistance);

            if (Mathf.Abs(dir.x) > 0.01f)
                ctx.FacingRight = dir.x >= 0f;

            ctx.Body.linearVelocity = dir * moveSpeed + GetWobble(ctx) + Separation(ctx);
        }

        private Vector2 GetDesiredPosition(EnemyContext ctx)
        {
            Vector2 target = ctx.TargetPosition;
            float baseY = target.y;

            if (ignoreTargetJumps)
            {
                Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                bool grounded = player != null && player.Controller != null && player.Controller.IsGrounded;

                if (grounded || float.IsNaN(ctx.TargetGroundY)) ctx.TargetGroundY = target.y;
                baseY = ctx.TargetGroundY;
            }

            float side = target.x <= ctx.Self.position.x ? 1f : -1f;
            return new Vector2(target.x + hoverPosition.x * side, baseY + hoverPosition.y);
        }

        private Vector2 Separation(EnemyContext ctx) =>
            EnemySeparation.GetPush(ctx, separationRadius, separationStrength, separationMask);

        private static void FaceTarget(EnemyContext ctx)
        {
            float dx = ctx.TargetPosition.x - ctx.Self.position.x;
            if (Mathf.Abs(dx) > 0.05f) ctx.FacingRight = dx > 0f;
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