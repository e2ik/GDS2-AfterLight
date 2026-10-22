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
            Vector2 origin = EnemyTerrainProbe.GetBodyBounds(ctx).center;
            bool alreadyTracking = ctx.Target != null && ctx.TargetVisible;
            float radius = alreadyTracking ? Mathf.Max(detectionRadius, loseRadius) : detectionRadius;

            Collider2D hit = Physics2D.OverlapCircle(origin, radius, targetMask);

            if (hit != null)
            {
                bool isBlocked = IsLineOfSightBlocked(origin, hit.bounds.center, obstructionMask);

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

        private static readonly RaycastHit2D[] losHits = new RaycastHit2D[8];

        private static bool IsLineOfSightBlocked(Vector2 origin, Vector2 target, LayerMask mask)
        {
            if (mask.value == 0) return false;

            ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = mask };
            int count = Physics2D.Linecast(origin, target, filter, losHits);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = losHits[i].collider;
                if (col == null) continue;
                if (col.usedByEffector || col.GetComponentInParent<PlatformEffector2D>() != null) continue;
                if (col.GetComponentInParent<DropThroughPlatform>() != null) continue;
                if (col.GetComponentInParent<AirOnlyCollisionPlatform>() != null) continue;
                return true;
            }

            return false;
        }
    }
}