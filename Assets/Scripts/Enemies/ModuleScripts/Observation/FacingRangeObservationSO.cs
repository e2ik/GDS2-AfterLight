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
            if (hit == null)
            {
                ctx.TargetVisible = false;
                return;
            }

            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            bool losBlocked = Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, obstructionMask);

            ctx.TargetVisible = !losBlocked;
            ctx.Target = hit.transform;
            ctx.TargetPosition = hit.transform.position;
        }
    }
}
