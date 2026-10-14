using Enemies.ModuleScripts.Movement;
using UnityEngine;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Chase")]
    public class ChaseMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float facingDeadZone = 0.15f;
        [SerializeField] private MoveWindupSettings windup = new MoveWindupSettings();

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            MoveWindup.BeginTick(ctx, windup);
            bool facingBefore = ctx.FacingRight;

            if (ctx.IsAttacking)
            {
                MoveWindup.Reset(ctx);
                if (!ctx.IgnoreTerrainChecks)
                    EnemyTerrainProbe.StopAtEdge(ctx);
                return;
            }

            if (ctx.Target == null)
            {
                MoveWindup.Process(ctx, windup, 0f, facingBefore, deltaTime);
                ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
                return;
            }

            float diff = ctx.Target.position.x - ctx.Self.position.x;
            bool arrived = Mathf.Abs(diff) <= facingDeadZone;
            int dir = arrived ? (ctx.FacingRight ? 1 : -1) : (diff >= 0f ? 1 : -1);

            ctx.FacingRight = dir > 0;

            float moveX = ctx.TargetInRange || arrived ? 0f : dir * moveSpeed;

            if (moveX != 0f && !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, dir))
            {
                moveX = 0f;
                ctx.LastChaseBlockedTime = Time.time;
            }

            moveX = MoveWindup.Process(ctx, windup, moveX, facingBefore, deltaTime);
            ctx.Body.linearVelocity = new Vector2(moveX, ctx.Body.linearVelocity.y);
        }
    }
}