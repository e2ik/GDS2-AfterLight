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
        
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            int dir = ctx.FacingRight ? 1 : -1;
            Vector2 origin = ctx.Self.position;

            bool groundAhead = Physics2D.Raycast(origin + Vector2.right * dir * edgeCheckDistance, Vector2.down,
                groundCheckDepth, groundMask);
            
            bool wallAhead = Physics2D.Raycast(origin, Vector2.right * dir, wallCheckDistance, groundMask);

            if (!groundAhead || wallAhead)
            {
                ctx.FacingRight = !ctx.FacingRight;
                dir = -dir;
            }

            ctx.Body.linearVelocity = new Vector2(dir * moveSpeed, ctx.Body.linearVelocity.y);
        }
    }
}
