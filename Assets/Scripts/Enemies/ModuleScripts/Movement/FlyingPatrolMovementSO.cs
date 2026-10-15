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

        [Header("Drift")]
        [SerializeField] private bool driftNearby = true;
        [SerializeField, Min(0f)] private float minDriftDistance = 1f;
        [SerializeField, Min(0f)] private float maxDriftDistance = 2.5f;
        [SerializeField, Range(0f, 1f)] private float verticalDriftScale = 0.4f;

        [Header("Return Home")]
        [SerializeField, Min(0.1f)] private float returnSpeedMultiplier = 2f;
        
        [Header("Separation")]
        [SerializeField, Min(0f)] private float separationRadius = 1.2f;
        [SerializeField, Min(0f)] private float separationStrength = 3f;
        [SerializeField] private LayerMask separationMask;

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
                ctx.Body.linearVelocity = GetWobble(ctx) + Separation(ctx);

                if (IsAwayFromHome(ctx))
                {
                    PickNewDestination(ctx);
                    return;
                }
                
                ctx.PatrolWaitTimer -= deltaTime;

                if (ctx.PatrolWaitTimer <= 0f)
                    PickNewDestination(ctx);
                
                return;
            }

            Vector2 dir = NavMeshFlightUtility.GetSteeringDirection(ctx.NavPath, ref ctx.PathCornerIndex,
                ctx.Self.position, cornerReachDistance);

            if (Mathf.Abs(dir.x) > 0.01f)
                ctx.FacingRight = dir.x >= 0f;

            float speed = IsAwayFromHome(ctx) ? moveSpeed * returnSpeedMultiplier : moveSpeed;
            ctx.Body.linearVelocity = dir * speed + GetWobble(ctx) + Separation(ctx);
        }

        private Vector2 Separation(EnemyContext ctx) =>
            EnemySeparation.GetPush(ctx, separationRadius, separationStrength, separationMask);

        private bool IsAwayFromHome(EnemyContext ctx)
        {
            return Vector2.Distance(ctx.Self.position, ctx.HomePosition) > patrolRadius;
        }

        private Vector2 GetWobble(EnemyContext ctx)
        {
            float t = Time.time * wobbleFrequency;
            float noiseX = Mathf.PerlinNoise(ctx.NoiseSeed, t) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(ctx.NoiseSeed + 100f, t) * 2f - 1f;
            return new Vector2(noiseX, noiseY) * wobbleAmplitude;
        }

        private Vector2 PickCandidate(EnemyContext ctx)
        {
            Vector2 home = ctx.HomePosition;

            if (IsAwayFromHome(ctx))
                return home + Random.insideUnitCircle * Mathf.Min(patrolRadius, maxDriftDistance);

            if (!driftNearby)
                return home + Random.insideUnitCircle * patrolRadius;

            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(minDriftDistance, maxDriftDistance);
            offset.y *= verticalDriftScale;
            Vector2 candidate = (Vector2)ctx.Self.position + offset;

            if (Vector2.Distance(candidate, home) > patrolRadius)
                candidate = (Vector2)ctx.Self.position + ((home - (Vector2)ctx.Self.position).normalized * offset.magnitude);

            return candidate;
        }

        private void PickNewDestination(EnemyContext ctx)
        {
            Vector2 candidate = PickCandidate(ctx);

            if(NavMeshFlightUtility.TrySamplePoint(candidate, navMeshSampleDistance, out Vector2 validPoint) && 
               NavMeshFlightUtility.TryCalculatePath(ctx.Self.position, validPoint, ctx.NavPath))
                ctx.PathCornerIndex = 0;
            else
                ctx.NavPath.ClearCorners();
            
            ctx.PatrolWaitTimer = Random.Range(minWaitAtWaypoint, maxWaitAtWaypoint);
            ctx.reachedTarget = false;
            ctx.HasHoverDestination = false;

        }
    }
}