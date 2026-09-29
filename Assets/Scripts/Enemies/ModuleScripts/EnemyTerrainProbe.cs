using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Enemies
{
    public static class EnemyTerrainProbe
    {
        private const float Skin = 0.05f;
        private const float WallNormalThreshold = 0.7f;

        private static readonly Dictionary<Rigidbody2D, Collider2D[]> bodyColliderCache = new();
        private static readonly Dictionary<Transform, Collider2D> hurtBoxCache = new();
        private static readonly List<Collider2D> attachedBuffer = new();

        public static bool IsBlocked(EnemyContext ctx, int dir, LayerMask mask, float edgeCheckDistance, float groundCheckDepth, float wallCheckDistance, float wallCheckHeight)
        {
            if (mask == 0 || dir == 0) return false;

            return !HasGroundAhead(ctx, dir, mask, edgeCheckDistance, groundCheckDepth)
                   || HasWallAhead(ctx, dir, mask, wallCheckDistance, wallCheckHeight);
        }

        public static void StopAtEdge(EnemyContext ctx, LayerMask mask, float edgeCheckDistance, float groundCheckDepth, float wallCheckDistance, float wallCheckHeight)
        {
            float velocityX = ctx.Body.linearVelocity.x;
            if (Mathf.Abs(velocityX) < 0.01f) return;

            int dir = velocityX > 0f ? 1 : -1;
            if (IsBlocked(ctx, dir, mask, edgeCheckDistance, groundCheckDepth, wallCheckDistance, wallCheckHeight))
            {
                ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
            }
        }

        public static bool HasGroundBelow(EnemyContext ctx, LayerMask mask, float groundCheckDepth)
        {
            Bounds body = GetBodyBounds(ctx);
            Vector2 origin = new Vector2(body.center.x, body.min.y + Skin);
            return TryCast(ctx, origin, Vector2.down, groundCheckDepth + Skin, mask, out _);
        }

        public static bool HasGroundAhead(EnemyContext ctx, int dir, LayerMask mask, float edgeCheckDistance, float groundCheckDepth)
        {
            Bounds body = GetBodyBounds(ctx);
            float frontX = FrontEdge(body, dir);
            Vector2 origin = new Vector2(frontX + dir * edgeCheckDistance, body.min.y + Skin);
            float distance = groundCheckDepth + Skin;

            bool groundAhead = TryCast(ctx, origin, Vector2.down, distance, mask, out _);
            Debug.DrawRay(origin, Vector2.down * distance, groundAhead ? Color.green : Color.red);
            return groundAhead;
        }

        public static bool HasWallAhead(EnemyContext ctx, int dir, LayerMask mask, float wallCheckDistance, float wallCheckHeight)
        {
            if (dir == 0) return false;

            Bounds body = GetBodyBounds(ctx);
            float frontX = FrontEdge(body, dir);
            Vector2 castDir = Vector2.right * dir;
            Vector2 origin = new Vector2(frontX - dir * Skin, body.min.y + Mathf.Max(wallCheckHeight, Skin * 2f));
            float distance = wallCheckDistance + Skin;

            bool wallHit = TryCast(ctx, origin, castDir, distance, mask, out RaycastHit2D hit)
                           && Mathf.Abs(hit.normal.x) >= WallNormalThreshold;

            Debug.DrawRay(origin, castDir * distance, wallHit ? Color.green : Color.red);
            return wallHit;
        }

        private static float FrontEdge(Bounds body, int dir)
        {
            if (dir > 0) return body.max.x;
            if (dir < 0) return body.min.x;
            return body.center.x;
        }

        private static bool TryCast(EnemyContext ctx, Vector2 origin, Vector2 direction, float distance, LayerMask mask, out RaycastHit2D closest)
        {
            closest = default;
            bool found = false;

            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, mask);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (ctx.Body != null && hit.rigidbody == ctx.Body) continue;

                if (!found || hit.distance < closest.distance)
                {
                    closest = hit;
                    found = true;
                }
            }

            return found;
        }

        public static Bounds GetBodyBounds(EnemyContext ctx)
        {
            bool hasBounds = false;
            Bounds bounds = default;

            if (ctx.Body != null)
            {
                foreach (Collider2D col in GetBodyColliders(ctx.Body))
                {
                    if (col == null || !col.enabled) continue;

                    if (!hasBounds)
                    {
                        bounds = col.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                }
            }

            if (hasBounds) return bounds;

            Collider2D hurtBox = GetHurtBox(ctx.Self);
            if (hurtBox != null) return hurtBox.bounds;

            return new Bounds(ctx.Self.position, Vector3.zero);
        }

        private static Collider2D[] GetBodyColliders(Rigidbody2D body)
        {
            if (bodyColliderCache.TryGetValue(body, out Collider2D[] cached) && cached.Length > 0)
                return cached;

            PruneDestroyed();

            attachedBuffer.Clear();
            body.GetAttachedColliders(attachedBuffer);
            Collider2D[] solid = attachedBuffer.Where(c => c != null && !c.isTrigger).ToArray();

            bodyColliderCache[body] = solid;
            return solid;
        }

        private static Collider2D GetHurtBox(Transform self)
        {
            if (hurtBoxCache.TryGetValue(self, out Collider2D cached) && cached != null)
                return cached;

            PruneDestroyed();

            Collider2D found = self.GetComponentsInChildren<Collider2D>(true)
                .FirstOrDefault(c => c.gameObject.name == "HurtBox");

            hurtBoxCache[self] = found;
            return found;
        }

        private static void PruneDestroyed()
        {
            if (bodyColliderCache.Count > 64)
            {
                List<Rigidbody2D> staleBodies = bodyColliderCache.Keys.Where(k => k == null).ToList();
                foreach (Rigidbody2D key in staleBodies) bodyColliderCache.Remove(key);
            }

            if (hurtBoxCache.Count > 64)
            {
                List<Transform> staleTransforms = hurtBoxCache.Keys.Where(k => k == null).ToList();
                foreach (Transform key in staleTransforms) hurtBoxCache.Remove(key);
            }
        }
    }
}