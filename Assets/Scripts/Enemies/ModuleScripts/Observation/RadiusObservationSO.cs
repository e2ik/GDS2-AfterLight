using UnityEngine;

namespace Enemies.ModuleScripts.Observation
{
    [CreateAssetMenu(menuName = "Enemies/Observation/Radius")]
    public class RadiusObservationSO : EnemyObservationSO
    {
        [SerializeField] private float detectionRadius = 5f;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstructionMask;

        [Header("Keep Aggro")]
        [SerializeField, Min(0f)] private float loseRadius = 9f;
        [SerializeField, Min(0f)] private float memoryDuration = 2.5f;
        
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            Vector2 origin = ctx.Self.position;
            bool alreadyTracking = ctx.Target != null && ctx.TargetVisible;
            float radius = alreadyTracking ? Mathf.Max(detectionRadius, loseRadius) : detectionRadius;

            Collider2D hit = Physics2D.OverlapCircle(origin, radius, targetMask);

            if (hit != null)
            {
                Vector2 toTarget = (Vector2)hit.transform.position - origin;
                bool isBlocked = obstructionMask != 0 &&
                                 Physics2D.Raycast(origin, toTarget.normalized, toTarget.magnitude, obstructionMask);

                if (!isBlocked)
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

            if (ctx.Target != null && ctx.TimeSinceTargetSeen <= memoryDuration)
            {
                ctx.TargetVisible = true;
                ctx.TargetPosition = ctx.LastKnownTargetPosition;
                return;
            }

            ctx.TargetVisible = false;
            ctx.Target = null;
        }
    }
}