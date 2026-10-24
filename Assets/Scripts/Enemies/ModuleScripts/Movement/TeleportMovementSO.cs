using System.Collections.Generic;
using FMODUnity;
using Unity.VisualScripting;
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
        [SerializeField, Min(0.1f)] private float animationTimeout = 2f;

        [Header("Defense")]
        [SerializeField] private bool invulnerableWhileTeleporting = true;
        [SerializeField] private bool disableCollisionWhileTeleporting = true;
        [Header("SFX")]
        [SerializeField] private EventReference disappearSound;
        [SerializeField] private EventReference reappearSound;

        public void Begin(EnemyContext ctx)
        {
            ctx.Body.linearVelocity = Vector2.zero;
            ctx.TeleportDestination = ClampInsideBounds(ctx, PickDestination(ctx));
            MarkUsed(ctx);
            if (invulnerableWhileTeleporting && ctx.Health != null) ctx.Health.Invulnerable = true;
            if (disableCollisionWhileTeleporting) ctx.Body.simulated = false;
            ctx.TeleportStartedTime = Time.time;
            ctx.Animator.SetTrigger(teleportOutTrigger);
            AudioManager.PlaySFX(disappearSound);
            ctx.TeleportPhase = TeleportPhase.TeleportingOut;
            ctx.TeleportTimer = animationTimeout;
        }

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            ctx.Body.linearVelocity = Vector2.zero;
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);

            switch (ctx.TeleportPhase)
            {
                case TeleportPhase.TeleportingOut:
                    ctx.TeleportTimer -= deltaTime;
                    if ((state.IsName(teleportOutStateName) && state.normalizedTime >= 1f) || ctx.TeleportTimer <= 0f)
                    {
                        Vector2 destination = ctx.TeleportDestination;
                        SetHidden(ctx, true);
                        ctx.Self.position = destination;
                        ctx.TeleportPhase = TeleportPhase.Hidden;
                        ctx.TeleportTimer = hiddenDuration;
                    }
                    break;

                case TeleportPhase.Hidden:
                    ctx.TeleportTimer -= deltaTime;
                    if (ctx.TeleportTimer <= 0f)
                    {
                        AudioManager.PlaySFX(reappearSound);
                        SetHidden(ctx, false);
                        ctx.Animator.SetTrigger(teleportInTrigger);
                        ctx.TeleportPhase = TeleportPhase.TeleportingIn;
                        ctx.TeleportTimer = animationTimeout;
                    }
                    break;

                case TeleportPhase.TeleportingIn:
                    ctx.TeleportTimer -= deltaTime;
                    if ((state.IsName(teleportInStateName) && state.normalizedTime >= 0.9f) || ctx.TeleportTimer <= 0f)
                    {
                        SetHidden(ctx, false);
                        ctx.Body.simulated = true;
                        ctx.TeleportPhase = TeleportPhase.Finished;
                        if (ctx.Health != null) ctx.Health.Invulnerable = false;
                        ctx.TeleportStartedTime = float.NegativeInfinity;
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
            ctx.Body.simulated = !hidden && !disableCollisionWhileTeleporting;
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

            float halfWidth = body.extents.x;
            float minX = Mathf.Min(bounds.min.x + halfWidth, bounds.center.x);
            float maxX = Mathf.Max(bounds.max.x - halfWidth, bounds.center.x);

            for (int attempt = 0; attempt < maxRerollAttempts; attempt++)
            {
                float x = Random.Range(minX, maxX);
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

        private static readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>();
        private static readonly List<Collider2D> overlapResults = new List<Collider2D>();

        private bool TryValidate(EnemyContext ctx, Vector2 candidate, bool useGround, Bounds body, bool hasTarget, Vector2 targetPos, float minDistSqr, out Vector2 destination)
        {
            destination = candidate;

            if (!useGround)
            {
                if (hasTarget && (candidate - targetPos).sqrMagnitude < minDistSqr) return false;
                if (!NavMeshFlightUtility.TrySamplePoint(candidate, navMeshSampleDistance, out destination)) return false;
                if (!InsideBounds(ctx, destination, body)) return false;
                return !hasTarget || (destination - targetPos).sqrMagnitude >= minDistSqr;
            }

            float startHeight = body.size.y;
            Vector2 start = candidate + Vector2.up * startHeight;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundMask };
            groundHits.Clear();
            Physics2D.Raycast(start, Vector2.down, filter, groundHits, groundCheckDepth + startHeight);

            float preferredY = matchTargetY && hasTarget ? targetPos.y : candidate.y;
            groundHits.Sort((a, b) => Mathf.Abs(a.point.y - preferredY).CompareTo(Mathf.Abs(b.point.y - preferredY)));

            float feetOffset = ctx.Self.position.y - body.min.y;
            Vector2 bodyCenterOffset = (Vector2)body.center - (Vector2)ctx.Self.position;

            foreach (RaycastHit2D hit in groundHits)
            {
                if (hit.collider == null || hit.distance <= 0f || hit.normal.y < 0.5f) continue;

                Vector2 landing = new Vector2(candidate.x, hit.point.y + feetOffset);
                Vector2 bodyCenterAtLanding = landing + bodyCenterOffset + Vector2.up * 0.05f;

                if (IsBlockedBySolidGround(bodyCenterAtLanding, (Vector2)body.size * 0.9f)) continue;
                if (!InsideBounds(ctx, landing, body)) continue;
                if (hasTarget && (landing - targetPos).sqrMagnitude < minDistSqr) continue;

                destination = landing;
                return true;
            }

            return false;
        }

        private bool IsBlockedBySolidGround(Vector2 center, Vector2 size)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundMask };
            overlapResults.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, overlapResults);

            foreach (Collider2D col in overlapResults)
            {
                if (col == null || IsPassThrough(col)) continue;
                return true;
            }

            return false;
        }

        private static bool IsPassThrough(Collider2D col)
        {
            if (col.usedByEffector || col.GetComponentInParent<PlatformEffector2D>() != null) return true;
            if (col.GetComponentInParent<DropThroughPlatform>() != null) return true;
            return col.GetComponentInParent<AirOnlyCollisionPlatform>() != null;
        }

        private bool InsideBounds(EnemyContext ctx, Vector2 destination, Bounds body)
        {
            Bounds bounds = ctx.BossBounds.WorldBounds;
            Vector2 offset = (Vector2)body.center - (Vector2)ctx.Self.position;
            Vector2 center = destination + offset;
            Vector2 extents = body.extents;

            return center.x - extents.x >= bounds.min.x && center.x + extents.x <= bounds.max.x
                && center.y >= bounds.min.y && center.y <= bounds.max.y;
        }

        private Vector2 ClampInsideBounds(EnemyContext ctx, Vector2 destination)
        {
            if (ctx.BossBounds == null) return destination;

            Bounds bounds = ctx.BossBounds.WorldBounds;
            Bounds body = EnemyTerrainProbe.GetBodyBounds(ctx);
            Vector2 offset = (Vector2)body.center - (Vector2)ctx.Self.position;
            Vector2 center = destination + offset;

            float minX = bounds.min.x + body.extents.x;
            float maxX = bounds.max.x - body.extents.x;

            center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : bounds.center.x;
            center.y = Mathf.Clamp(center.y, bounds.min.y, bounds.max.y);

            return center - offset;
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