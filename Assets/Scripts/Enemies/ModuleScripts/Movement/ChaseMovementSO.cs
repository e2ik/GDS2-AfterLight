using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Chase")]
    public class ChaseMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float facingDeadZone = 0.15f;
        [SerializeField] private float stopBuffer = 0.1f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking) return;
            
            if (ctx.Target == null) return;

            float diff = ctx.TargetPosition.x - ctx.Self.position.x;
            
            if(Mathf.Abs(diff) > facingDeadZone)
                ctx.FacingRight = diff >= 0f;
            
            int dir = ctx.FacingRight ? 1 : -1;

            float distance = Vector2.Distance(ctx.Self.position, ctx.TargetPosition);
            bool closeEnoughToStop = distance <= ctx.AttackStopDistance + stopBuffer;

            float moveX = closeEnoughToStop ? 0f : dir * moveSpeed;
            ctx.Body.linearVelocity = new Vector2(moveX, ctx.Body.linearVelocity.y);
        }

    }
}
