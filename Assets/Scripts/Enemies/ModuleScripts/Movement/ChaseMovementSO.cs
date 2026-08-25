using UnityEngine;
using UnityEngine.UIElements;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Chase")]
    public class ChaseMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3.5f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.Target == null) return;

            float diff = ctx.TargetPosition.x - ctx.Self.position.x;
            ctx.FacingRight = diff >= 0f;
            int dir = ctx.FacingRight ? 1 : -1;

            ctx.Body.linearVelocity = new Vector2(dir * moveSpeed, ctx.Body.linearVelocity.y);
        }

    }
}
