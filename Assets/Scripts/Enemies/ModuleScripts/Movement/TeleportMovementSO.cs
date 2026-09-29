using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    public enum TeleportPhase {TeleportingOut, Hidden, TeleportingIn, Finished}

    [CreateAssetMenu(menuName = "Enemies/Movement/Teleport")]
    public class TeleportMovementSO : EnemyMovementSO
    {
        [Header("Destination")]
        [SerializeField] private bool changeY = false;
        [SerializeField] private bool matchTargetY = false;
        [SerializeField] private float minDistanceFromTarget = 3f;
        [SerializeField] private float navMeshSampleDistance = 1f;
        [SerializeField] private int maxRerollAttempts = 20;

        [Header("Ground Placement")]
        [SerializeField] private bool requireGround = true;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float groundCheckDepth = 3f;
        [SerializeField] private float nearTargetMaxDistance = 6f;

        [Header("Teleport To Target")]
        [SerializeField] private float toTargetMinDistance = 1f;
        [SerializeField] private float toTargetMaxDistance = 2f;

        [Header("Animation")]
        [SerializeField] private string teleportOutTrigger = "TeleportOut";
        [SerializeField] private string teleportInTrigger = "TeleportIn";
        [SerializeField] private string teleportOutStateName = "TeleportOut";
        [SerializeField] private string teleportInStateName = "TeleportIn";
        [SerializeField] private float hiddenDuration = 1.5f;


        public void Begin(EnemyContext ctx)
        {
            ctx.Body.linearVelocity = Vector2.zero;
            ctx.TeleportDestination = PickDestination(ctx);
            MarkUsed(ctx);

            ctx.Animator.SetTrigger(teleportOutTrigger);
            ctx.TeleportPhase = TeleportPhase.TeleportingOut;
        }

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            ctx.Body.linearVelocity = Vector2.zero;
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);

            switch (ctx.TeleportPhase)
            {
                case TeleportPhase.TeleportingOut:
                    if (state.IsName(teleportOutStateName) && state.normalizedTime >= 1f)
                    {
                        SetHidden(ctx, true);
                        ctx.Self.position = ctx.TeleportDestination;
                        ctx.TeleportPhase = TeleportPhase.Hidden;
                        ctx.TeleportTimer = hiddenDuration;
                    }
                    break;

                case TeleportPhase.Hidden:
                    ctx.TeleportTimer -= deltaTime;
                    if (ctx.TeleportTimer <= 0f)
                    {
                        SetHidden(ctx, false);
                        ctx.Animator.SetTrigger(teleportInTrigger);
                        ctx.TeleportPhase = TeleportPhase.TeleportingIn;
                    }
                    break;

                case TeleportPhase.TeleportingIn:
                    if (state.IsName(teleportInStateName) && state.normalizedTime >= 0.9f)
                    {
                        ctx.TeleportPhase = TeleportPhase.Finished;
                        MarkUsed(ctx);
                    }
                    break;
            }
        }

        private void MarkUsed(EnemyContext ctx)
        {
            if (ctx.Self.TryGetComponent(out Enemy enemy))
                enemy.MarkTeleportUsed();
        }

        public bool IsFinished(EnemyContext ctx) => ctx.TeleportPhase == TeleportPhase.Finished;

        private void SetHidden(EnemyContext ctx, bool hidden)
        {
            if (ctx.SpriteRenderer != null)
                ctx.SpriteRenderer.enabled = !hidden;

            ctx.Body.simulated = !hidden;
        }

        private Vector2 PickDestination(EnemyContext ctx)
        {
            if (ctx.BossBounds == null)
            {
                Debug.LogWarning($"{name}: no BossBounds assigned on this enemies context");
                return ctx.Self.position;
            }

            Bounds bounds = ctx.BossBounds.WorldBounds;
            bool hasTarget = ctx.Target != null;
            Vector2 targetPos = hasTarget ? (Vector2)ctx.Target.position : ctx.TargetPosition;
            float minDistSqr = minDistanceFromTarget * minDistanceFromTarget;
            bool useGround = requireGround && groundMask != 0;
            Bounds body = EnemyTerrainProbe.GetBodyBounds(ctx);
            bool nearTargetFirst = ctx.ForceTeleportNearTarget && useGround && hasTarget;

            if (nearTargetFirst && TryPickNearTarget(ctx, bounds, body, targetPos, toTargetMinDistance, toTargetMaxDistance, out Vector2 nearDestination))
                return nearDestination;

            for (int attempt = 0; attempt < maxRerollAttempts; attempt++)
            {
                float x = Random.Range(bounds.min.x, bounds.max.x);
                float y = PickY(ctx, bounds, hasTarget, targetPos);

                if (TryValidate(ctx, new Vector2(x, y), useGround, body, hasTarget, targetPos, minDistSqr, out Vector2 destination))
                    return destination;
            }

            if (!nearTargetFirst && useGround && hasTarget && TryPickNearTarget(ctx, bounds, body, targetPos, minDistanceFromTarget, nearTargetMaxDistance, out nearDestination))
                return nearDestination;

            Debug.LogWarning($"{name}: couldn't find a valid teleport destination, staying in place");
            return ctx.Self.position;
        }

        private bool TryPickNearTarget(EnemyContext ctx, Bounds bounds, Bounds body, Vector2 targetPos, float minDistance, float maxDistance, out Vector2 destination)
        {
            minDistance = Mathf.Max(0f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            float minDistSqr = minDistance * minDistance;

            for (int attempt = 0; attempt < maxRerollAttempts; attempt++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float offset = Random.Range(minDistance, maxDistance);
                float x = Mathf.Clamp(targetPos.x + side * offset, bounds.min.x, bounds.max.x);

                float y = Mathf.Clamp(targetPos.y, bounds.min.y, bounds.max.y);

                if (TryValidate(ctx, new Vector2(x, y), true, body, true, targetPos, minDistSqr, out destination))
                    return true;
            }

            destination = ctx.Self.position;
            return false;
        }

        private bool TryValidate(EnemyContext ctx, Vector2 candidate, bool useGround, Bounds body, bool hasTarget, Vector2 targetPos, float minDistSqr, out Vector2 destination)
        {
            destination = candidate;

            if (useGround)
            {
                if (Physics2D.OverlapPoint(candidate, groundMask)) return false;

                RaycastHit2D hit = Physics2D.Raycast(candidate, Vector2.down, groundCheckDepth, groundMask);
                if (hit.collider == null) return false;

                float feetOffset = ctx.Self.position.y - body.min.y;
                destination = new Vector2(candidate.x, hit.point.y + feetOffset);

                Vector2 bodyCenterOffset = (Vector2)body.center - (Vector2)ctx.Self.position;
                Vector2 bodyCenterAtDestination = destination + bodyCenterOffset + Vector2.up * 0.05f;
                if (Physics2D.OverlapBox(bodyCenterAtDestination, (Vector2)body.size * 0.9f, 0f, groundMask)) return false;
            }
            else
            {
                if (hasTarget && (candidate - targetPos).sqrMagnitude < minDistSqr) return false;
                if (!NavMeshFlightUtility.TrySamplePoint(candidate, navMeshSampleDistance, out destination)) return false;
            }

            if (!InsideBounds(ctx, destination)) return false;

            return !hasTarget || (destination - targetPos).sqrMagnitude >= minDistSqr;
        }

        private bool InsideBounds(EnemyContext ctx, Vector2 point)
        {
            Bounds bounds = ctx.BossBounds.WorldBounds;
            return point.x >= bounds.min.x && point.x <= bounds.max.x
                && point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        private float PickY(EnemyContext ctx, Bounds bounds, bool hasTarget, Vector2 targetPos)
        {
            if (matchTargetY && hasTarget)
                return Mathf.Clamp(targetPos.y, bounds.min.y, bounds.max.y);

            if (changeY)
                return Random.Range(bounds.min.y, bounds.max.y);

            return ctx.Self.position.y;
        }
    }
}