using UnityEngine;

namespace Enemies.ModuleScripts.Observation
{
    [CreateAssetMenu(menuName = "Enemies/Observation/Facing Range")]
    public class FacingRangeObservationSO : EnemyObservationSO
    {
        [SerializeField] private float viewDistance = 5f;
        [SerializeField] private float viewHeight = 1f;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask;

        [Header("Close-range awareness (any direction)")]
        [Tooltip("Radius around the enemy that detects a target regardless of facing direction, so a player sneaking up from behind still gets noticed.")]
        [SerializeField] private float proximityRadius = 1.25f;

        [Header("Aggro memory")]
        [Tooltip("How long (seconds) the enemy keeps chasing the last known position after losing sight, before fully giving up.")]
        [SerializeField] private float memoryDuration = 2.5f;


        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            Vector2 origin = ctx.Self.position;

            Collider2D hit = TryFacingCone(ctx, origin);
            if (hit == null)
                hit = Physics2D.OverlapCircle(origin, proximityRadius, targetMask);

            if (hit != null)
            {
                Vector2 toTarget = (Vector2)hit.transform.position - origin;
                bool losBlocked = Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, obstructionMask);

                if (!losBlocked)
                {
                    ctx.TimeSinceTargetSeen = 0f;
                    ctx.TargetVisible = true;
                    ctx.Target = hit.transform;
                    ctx.TargetPosition = hit.transform.position;
                    ctx.LastKnownTargetPosition = hit.transform.position;
                    return;
                }
            }

            ctx.TimeSinceTargetSeen += deltaTime;

            bool stillRemembers = ctx.Target != null && ctx.TimeSinceTargetSeen <= memoryDuration;

            if (stillRemembers)
            {
                ctx.TargetVisible = true;
                ctx.TargetPosition = ctx.LastKnownTargetPosition;
            }
            else
            {
                ctx.TargetVisible = false;
                ctx.Target = null;
            }
        }

        private Collider2D TryFacingCone(EnemyContext ctx, Vector2 origin)
        {
            int dir = ctx.FacingRight ? 1 : -1;
            Vector2 size = new Vector2(viewDistance, viewHeight);
            Vector2 center = origin + Vector2.right * dir * (viewDistance / 2f);

            var hit = Physics2D.OverlapBox(center, size, 0f, targetMask);
            //DebugDrawOverlapBox(center, size, 0f, hit ? Color.red : Color.green);
            return hit;
        }

        public void DebugDrawOverlapBox(Vector2 center, Vector2 size, float angle, Color color)
        {
            float radians = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 halfSize = size * 0.5f;

            Vector2[] localCorners = new Vector2[4] {
                new Vector2(-halfSize.x, -halfSize.y),
                new Vector2(halfSize.x, -halfSize.y),
                new Vector2(halfSize.x, halfSize.y),
                new Vector2(-halfSize.x, halfSize.y)
            };

            Vector2[] worldCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 rotated = new Vector2(
                    localCorners[i].x * cos - localCorners[i].y * sin,
                    localCorners[i].x * sin + localCorners[i].y * cos
                );
                worldCorners[i] = center + rotated;
            }

            Debug.DrawLine(worldCorners[0], worldCorners[1], color);
            Debug.DrawLine(worldCorners[1], worldCorners[2], color);
            Debug.DrawLine(worldCorners[2], worldCorners[3], color);
            Debug.DrawLine(worldCorners[3], worldCorners[0], color);
        }
    }
}