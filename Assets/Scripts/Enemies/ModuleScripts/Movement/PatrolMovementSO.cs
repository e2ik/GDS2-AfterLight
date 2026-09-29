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
        [SerializeField] private float wallCheckHeight = 0.1f;
        [SerializeField] private Vector2 wallCheckBoxSize = new Vector2(0.1f, 0.2f);

        [SerializeField] private LayerMask groundMask;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking)
            {
                EnemyTerrainProbe.StopAtEdge(ctx, groundMask, edgeCheckDistance, groundCheckDepth, wallCheckDistance, wallCheckHeight);
                return;
            }

            int dir = ctx.FacingRight ? 1 : -1;

            bool groundAhead = EnemyTerrainProbe.HasGroundAhead(ctx, dir, groundMask, edgeCheckDistance, groundCheckDepth);
            bool wallAhead = EnemyTerrainProbe.HasWallAhead(ctx, dir, groundMask, wallCheckDistance, wallCheckHeight);

            if (!groundAhead || wallAhead)
            {
                ctx.FacingRight = !ctx.FacingRight;
                dir = -dir;
            }

            ctx.Body.linearVelocity = new Vector2(dir * moveSpeed, ctx.Body.linearVelocity.y);
        }
    }
}