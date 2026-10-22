using System.Collections.Generic;
using UnityEngine;

namespace Enemies.ModuleScripts.Observation
{
    [CreateAssetMenu(menuName = "Enemies/Observation/Facing Range")]
    public class FacingRangeObservationSO : EnemyObservationSO
    {
        [SerializeField] private float viewDistance = 5f;
        [SerializeField] private float bottomMargin = 0.1f;
        [SerializeField] private float topMargin = 0.1f;
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
            Bounds body = EnemyTerrainProbe.GetBodyBounds(ctx);
            Vector2 origin = body.center;

            GetVisionBand(body, out float bandBottom, out float bandTop);

            Collider2D hit = TryFacingCone(ctx, body, bandBottom, bandTop);
            if (hit == null)
                hit = TryProximity(origin, bandBottom, bandTop);

            if (hit != null)
            {
                bool losBlocked = IsLineOfSightBlocked(origin, hit.bounds.center, obstructionMask);

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

        private void GetVisionBand(Bounds body, out float bottom, out float top)
        {
            bottom = body.min.y + bottomMargin;
            top = body.max.y - topMargin;
            if (top - bottom < 0.1f)
            {
                float mid = body.center.y;
                bottom = mid - 0.05f;
                top = mid + 0.05f;
            }
        }

        private static readonly List<Collider2D> proximityResults = new List<Collider2D>();

        private Collider2D TryProximity(Vector2 origin, float bottom, float top)
        {
            if (proximityRadius <= 0f) return null;

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = targetMask };
            Physics2D.OverlapCircle(origin, proximityRadius, filter, proximityResults);

            foreach (Collider2D col in proximityResults)
            {
                if (col == null) continue;
                Bounds bounds = col.bounds;
                if (bounds.max.y >= bottom && bounds.min.y <= top) return col;
            }

            return null;
        }

        private Collider2D TryFacingCone(EnemyContext ctx, Bounds body, float bottom, float top)
        {
            int dir = ctx.FacingRight ? 1 : -1;

            Vector2 size = new Vector2(viewDistance, top - bottom);
            Vector2 center = new Vector2(body.center.x + dir * (viewDistance / 2f), (top + bottom) * 0.5f);

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