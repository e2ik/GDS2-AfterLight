using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Patrol")]
    public class PatrolMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float edgeCheckDistance = 0.5f;
        [SerializeField] private float groundCheckDepth = 1f;
        [SerializeField] private float wallCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;

        private readonly Dictionary<Transform, Collider2D> hitBoxCache = new();

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking) return;

            int dir = ctx.FacingRight ? 1 : -1;
            Vector2 origin = ctx.Self.position;

            Vector2 groundOrigin = origin + Vector2.right * dir * edgeCheckDistance;
            Vector2 groundDirection = Vector2.down * groundCheckDepth;

            bool groundAhead = Physics2D.Raycast(groundOrigin, Vector2.down, groundCheckDepth, groundMask);
            Debug.DrawRay((Vector3)groundOrigin, (Vector3)groundDirection, groundAhead ? Color.green : Color.red);

            bool wallAhead = CheckWallAhead(ctx, origin, dir, groundMask);

            if (!groundAhead || wallAhead)
            {
                ctx.FacingRight = !ctx.FacingRight;
                dir = -dir;
            }

            ctx.Body.linearVelocity = new Vector2(dir * moveSpeed, ctx.Body.linearVelocity.y);
        }

    private bool CheckWallAhead(EnemyContext ctx, Vector2 origin, int dir, LayerMask mask)
    {
        Collider2D hitBox = GetHitBox(ctx.Self);
        Vector2 castDir = Vector2.right * dir;

        if (hitBox == null)
        {
            bool hit = Physics2D.Raycast(origin, castDir, wallCheckDistance, mask);
            Debug.DrawRay(origin, castDir * wallCheckDistance, hit ? Color.green : Color.red);
            return hit;
        }

        Bounds bounds = hitBox.bounds;

        // edge of the collider in the facing direction
        float edgeX = dir > 0 ? bounds.max.x : bounds.min.x;
        Vector2 edgeOrigin = new Vector2(edgeX, bounds.center.y);

        bool wallHit = Physics2D.Raycast(edgeOrigin, castDir, wallCheckDistance, mask);
        Debug.DrawRay(edgeOrigin, castDir * wallCheckDistance, wallHit ? Color.green : Color.red);

        return wallHit;
    }

        private Collider2D GetHitBox(Transform self)
        {
            if (hitBoxCache.TryGetValue(self, out Collider2D cached) && cached != null)
                return cached;

            Collider2D found = self.GetComponentsInChildren<Collider2D>(true)
                .FirstOrDefault(c => c.gameObject.name == "HitBox");

            hitBoxCache[self] = found;
            return found;
        }
    }
}