using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Flying Patrol")]
    public class FlyingPatrolMovementSO : EnemyMovementSO
    {
        [SerializeField] private float patrolRadius = 6f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float cornerReachDistance = 0.2f;
        [SerializeField] private float minWaitAtWaypoint = 1f;
        [SerializeField] private float maxWaitAtWaypoint = 3f;
        [SerializeField] private float navMeshSampleDistance = 2f;
        
        [Header("Wobble")] 
        [SerializeField] private float wobbleAmplitude = 0.5f;
        [SerializeField] private float wobbleFrequency = 1f;
        
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.IsAttacking) return;
            
            bool atDestination = ctx.NavPath.corners.Length == 0 ||
                                 ctx.PathCornerIndex >= ctx.NavPath.corners.Length - 1 &&
                                 Vector2.Distance(ctx.Self.position, ctx.NavPath.corners[^1]) <= cornerReachDistance;

            if (ctx.reachedTarget || atDestination)
            {
                ctx.reachedTarget = true;
                ctx.Body.linearVelocity = GetWobble(ctx);
                
                ctx.PatrolWaitTimer -= deltaTime;

                if (ctx.PatrolWaitTimer <= 0f)
                    PickNewDestination(ctx);
                
                return;
            }

            Vector2 dir = NavMeshFlightUtility.GetSteeringDirection(ctx.NavPath, ref ctx.PathCornerIndex,
                ctx.Self.position, cornerReachDistance);

            if (Mathf.Abs(dir.x) > 0.01f)
                ctx.FacingRight = dir.x >= 0f;

            ctx.Body.linearVelocity = dir * moveSpeed + GetWobble(ctx);
        }

        private Vector2 GetWobble(EnemyContext ctx)
        {
            float t = Time.time * wobbleFrequency;
            float noiseX = Mathf.PerlinNoise(ctx.NoiseSeed, t) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(ctx.NoiseSeed + 100f, t) * 2f - 1f;
            return new Vector2(noiseX, noiseY) * wobbleAmplitude;
        }

        private void PickNewDestination(EnemyContext ctx)
        {
            Vector2 candidate = (Vector2)ctx.HomePosition + Random.insideUnitCircle * patrolRadius;

            if(NavMeshFlightUtility.TrySamplePoint(candidate, navMeshSampleDistance, out Vector2 validPoint) && 
               NavMeshFlightUtility.TryCalculatePath(ctx.Self.position, validPoint, ctx.NavPath))
                ctx.PathCornerIndex = 0;
            else
                ctx.NavPath.ClearCorners();
            
            ctx.PatrolWaitTimer = Random.Range(minWaitAtWaypoint, maxWaitAtWaypoint);
            ctx.reachedTarget = false;

        }
    }
}
