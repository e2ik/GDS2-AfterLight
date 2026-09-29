using UnityEngine;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Chase")]
    public class ChaseMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float facingDeadZone = 0.15f;

        [Header("Edge & Wall Checks")]
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float edgeCheckDistance = 0.5f;
        [SerializeField] private float groundCheckDepth = 1f;
        [SerializeField] private float wallCheckDistance = 0.3f;
        [SerializeField] private float wallCheckHeight = 0.1f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking)
            {
                if (!ctx.IgnoreTerrainChecks)
                    EnemyTerrainProbe.StopAtEdge(ctx, groundMask, edgeCheckDistance, groundCheckDepth, wallCheckDistance, wallCheckHeight);
                return;
            }

            if (ctx.Target == null)
            {
                ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
                return;
            }

            float diff = ctx.Target.position.x - ctx.Self.position.x;
            bool arrived = Mathf.Abs(diff) <= facingDeadZone;

            if (!arrived)
                ctx.FacingRight = diff >= 0f;

            int dir = ctx.FacingRight ? 1 : -1;

            float moveX = ctx.TargetInRange || arrived ? 0f : dir * moveSpeed;

            if (moveX != 0f && !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, dir, groundMask, edgeCheckDistance, groundCheckDepth, wallCheckDistance, wallCheckHeight))
            {
                moveX = 0f;
                ctx.LastChaseBlockedTime = Time.time;
            }

            ctx.Body.linearVelocity = new Vector2(moveX, ctx.Body.linearVelocity.y);
        }
    }
}