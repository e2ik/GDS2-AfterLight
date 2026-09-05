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

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            int dir = ctx.FacingRight ? 1 : -1;
            Vector2 origin = ctx.Self.position;
            Vector2 size = new Vector2(viewDistance, viewHeight);
            Vector2 center = origin + Vector2.right * dir * (viewDistance / 2f);

            var hit = Physics2D.OverlapBox(center, size, 0f, targetMask);
            //DebugDrawOverlapBox(center, size, 0f, hit ? Color.red : Color.green);
            
            if (hit == null)
            {
                ctx.TargetVisible = false;
                ctx.Target = null;
                return;
            }

            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            bool losBlocked = Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, obstructionMask);

            ctx.TargetVisible = !losBlocked;
            ctx.Target = hit.transform;
            ctx.TargetPosition = hit.transform.position;
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
