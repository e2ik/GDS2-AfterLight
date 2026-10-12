using UnityEngine;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Patrol")]
    public class PatrolMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 2f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking)
            {
                if (!ctx.IgnoreTerrainChecks) EnemyTerrainProbe.StopAtEdge(ctx);
                return;
            }

            int dir = ctx.FacingRight ? 1 : -1;

            if (!ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, dir))
            {
                ctx.FacingRight = !ctx.FacingRight;
                dir = -dir;
            }

            ctx.Body.linearVelocity = new Vector2(dir * moveSpeed, ctx.Body.linearVelocity.y);
        }
    }
}