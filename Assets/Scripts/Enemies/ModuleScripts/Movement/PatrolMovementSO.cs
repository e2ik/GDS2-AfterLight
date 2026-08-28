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
            if (ctx.IsAttacking) return;
            
            int dir = ctx.FacingRight ? 1 : -1;
            Vector2 origin = ctx.Self.position;

            Vector2 groundOrigin = origin + Vector2.right * dir * edgeCheckDistance;
            Vector2 groundDirection = Vector2.down * groundCheckDepth;

            bool groundAhead = Physics2D.Raycast(groundOrigin, Vector2.down, groundCheckDepth, groundMask);
            Debug.DrawRay((Vector3)groundOrigin, (Vector3)groundDirection, groundAhead ? Color.green : Color.red);
            
            Vector2 wallOrigin = origin;
            Vector2 wallDirection = Vector2.right * dir * wallCheckDistance;

            bool wallAhead = Physics2D.Raycast(wallOrigin, Vector2.right * dir, wallCheckDistance, groundMask);
            Debug.DrawRay((Vector3)wallOrigin, (Vector3)wallDirection, wallAhead ? Color.green : Color.red);

            if (!groundAhead || wallAhead)
            {
                ctx.FacingRight = !ctx.FacingRight;
                dir = -dir;
            }

            ctx.Body.linearVelocity = new Vector2(dir * moveSpeed, ctx.Body.linearVelocity.y);
        }
    }
}
