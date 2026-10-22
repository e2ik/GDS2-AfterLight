using UnityEngine;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Chase")]
    public class ChaseMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float facingDeadZone = 0.15f;

        [Header("Back Up")]
        [SerializeField] private float backUpDistance = 0f;
        [SerializeField] private float backUpSpeed = 2f;
        [SerializeField] private bool backUpFacesTarget = true;
        [SerializeField, Min(0f)] private float backUpMaxHeightDifference = 1.5f;

        [Header("Unreachable")]
        [SerializeField, Min(0f)] private float targetBelowHeight = 1.5f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking)
            {
                if (!ctx.IgnoreTerrainChecks)
                    EnemyTerrainProbe.StopAtEdge(ctx);
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

            float heightDiff = Mathf.Abs(ctx.Target.position.y - ctx.Self.position.y);

            if (targetBelowHeight > 0f && ctx.Self.position.y - ctx.Target.position.y > targetBelowHeight)
                ctx.LastChaseBlockedTime = Time.time;
            bool sameLevel = backUpMaxHeightDifference <= 0f || heightDiff <= backUpMaxHeightDifference;

            if (backUpDistance > 0f && sameLevel && Mathf.Abs(diff) < backUpDistance)
            {
                int backDir = -dir;
                if (!ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, backDir))
                {
                    ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
                    return;
                }

                if (!backUpFacesTarget) ctx.FacingRight = backDir > 0;
                ctx.Body.linearVelocity = new Vector2(backDir * backUpSpeed, ctx.Body.linearVelocity.y);
                return;
            }

            if (moveX != 0f && !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, dir))
            {
                moveX = 0f;
                ctx.LastChaseBlockedTime = Time.time;
            }

            ctx.Body.linearVelocity = new Vector2(moveX, ctx.Body.linearVelocity.y);
        }
    }
}