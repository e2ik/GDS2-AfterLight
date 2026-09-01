using UnityEngine;

namespace Enemies.ModuleScripts.Observation
{
    [CreateAssetMenu(menuName = "Enemies/Observation/Radius")]
    public class RadiusObservationSO : EnemyObservationSO
    {
        [SerializeField] private float detectionRadius = 5f;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask;
        
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            Collider2D hit = Physics2D.OverlapCircle(ctx.Self.position, detectionRadius, targetMask);
            if (hit == null)
            {
                ctx.TargetVisible = false;
                return;
            }

            Vector2 origin = ctx.Self.position;
            Vector2 toTarget = (Vector2)hit.transform.position - origin;

            bool isBlocked = obstructionMask != 0 &&
                             Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, obstructionMask);

            ctx.TargetVisible = !isBlocked;
            ctx.Target = hit.transform;
            ctx.TargetPosition = hit.transform.position;

        }
    }
}
