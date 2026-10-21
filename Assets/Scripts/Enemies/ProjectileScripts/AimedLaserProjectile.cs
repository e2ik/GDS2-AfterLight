using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class AimedLaserProjectile : Projectile
    {
        private enum LaserPhase { Travel, Warning, Firing }

        private struct PathSegment
        {
            public Vector2 Start;
            public Vector2 Direction;
            public float Length;
        }

        private class BeamSegment
        {
            public HitBox HitBox;
            public BoxCollider2D Collider;
            public LaserBeamVFX Vfx;
        }

        [Header("Travel")]
        [SerializeField, Min(0f)] private float travelDuration = 0f;
        [SerializeField] private bool bodyHitsPlayer = false;

        [Header("Aim")]
        [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.5f);
        [SerializeField] private bool trackDuringWarning = true;
        [SerializeField, Min(0f)] private float lockBeforeFire = 0.15f;
        [SerializeField, Min(0f)] private float aimTurnSpeed = 90f;
        [SerializeField, Range(0f, 180f)] private float startAngleOffset = 0f;

        [Header("Warning")]
        [SerializeField, Min(0f)] private float warningDelay = 0.6f;
        [SerializeField] private EventReference warningEvent;
        [SerializeField] private bool playAnticipation = true;
        [SerializeField, Min(0f)] private float anticipationLead = 0.3f;
        [SerializeField] private bool chargeDuringWarning = true;
        [SerializeField] private bool lerpWarningColor = true;
        [SerializeField] private Color warningStartColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color warningFireColor = new Color(1f, 0.1f, 0.05f, 1f);
        [SerializeField] private AnimationCurve warningColorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float warningStartWidthMultiplier = 3f;
        [SerializeField] private AnimationCurve warningWidthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Beam")]
        [SerializeField] private HitBox beamHitBox;
        [SerializeField] private float beamLength = 12f;
        [SerializeField] private float beamWidth = 0.4f;
        [SerializeField, Min(0f)] private float beamHitboxExtraWidth = 0.1f;
        [SerializeField] private float fireDuration = 0.5f;
        [SerializeField, Min(0f)] private float beamGrowDuration = 0.1f;
        [SerializeField] private AnimationCurve beamGrowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool hitboxGrowsWithBeam = true;
        [SerializeField] private LayerMask obstructMask;
        [SerializeField, Min(0)] private int maxBounces = 0;
        [SerializeField] private string impactEffect = "";
        [SerializeField] private AttackForce beamAttackForce = AttackForce.Heavy;
        [SerializeField] private EventReference fireEvent;
        [SerializeField] private SpriteRenderer bodySprite;
        [SerializeField] private bool hideBodyWhenFiring = false;
        [SerializeField, Min(0f)] private float bodyHideFadeDuration = 0.08f;
        [SerializeField] private bool fadeInBodyDuringWarning = true;
        [SerializeField] private AnimationCurve bodyFadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private LaserPhase phase;
        private float timer;
        private float growTimer;
        private float totalPathLength;
        private bool hitLanded;
        private bool anticipationPlayed;
        private float bodyBaseAlpha = -1f;
        private float bodyHideElapsed = -1f;

        private readonly List<BeamSegment> segments = new List<BeamSegment>();
        private readonly List<PathSegment> path = new List<PathSegment>();
        private readonly List<Vector2> impactPoints = new List<Vector2>();
        private readonly List<RaycastHit2D> obstructionHits = new List<RaycastHit2D>();
        private ContactFilter2D obstructionFilter;
        private readonly List<Collider2D> playerOverlapHits = new List<Collider2D>();

        private float BeamHitboxWidth => beamWidth + beamHitboxExtraWidth;

        public override bool HandlesOwnAnticipation => playAnticipation;

        protected override void OnLaunch(Vector2 initialVelocity)
        {
            BuildSegments();
            anticipationPlayed = false;

            Rb.gravityScale = 0f;
            Rb.linearVelocity = initialVelocity;

            if (!bodyHitsPlayer) HitBox.Disable();
            if (bodySprite == null) bodySprite = GetComponent<SpriteRenderer>();
            if (bodySprite != null)
            {
                if (bodyBaseAlpha < 0f) bodyBaseAlpha = bodySprite.color.a;
                bodySprite.enabled = true;
                SetBodyAlpha(fadeInBodyDuringWarning ? 0f : 1f);
            }

            ResetSegments();

            phase = LaserPhase.Travel;
            timer = travelDuration;
            if (travelDuration <= 0f) StartWarning();
        }

        private void BuildSegments()
        {
            if (beamHitBox == null) return;

            int needed = maxBounces + 1;
            if (segments.Count == 0) segments.Add(CreateSegment(beamHitBox));

            while (segments.Count < needed)
            {
                GameObject copy = Instantiate(beamHitBox.gameObject, beamHitBox.transform.parent);
                copy.name = $"{beamHitBox.gameObject.name} Bounce {segments.Count}";
                segments.Add(CreateSegment(copy.GetComponent<HitBox>()));
            }
        }

        private BeamSegment CreateSegment(HitBox hitBox)
        {
            BeamSegment segment = new BeamSegment
            {
                HitBox = hitBox,
                Collider = hitBox.GetComponent<BoxCollider2D>(),
                Vfx = hitBox.GetComponent<LaserBeamVFX>()
            };

            if (segment.Vfx != null) segment.Vfx.BeamTouched += HandleBeamTouched;
            return segment;
        }

        private void HandleBeamTouched(Collider2D other)
        {
            if (phase != LaserPhase.Firing) return;
            TryDamageEnemy(other);
        }

        private void ResetSegments()
        {
            foreach (BeamSegment segment in segments)
            {
                segment.HitBox.Disable();
                segment.Vfx?.Stop();
                segment.Vfx?.HideWarning();
            }
        }

        protected override bool OnHitTrigger(Collider2D other) => false;

        private void FixedUpdate()
        {
            switch (phase)
            {
                case LaserPhase.Travel:
                    timer -= Time.fixedDeltaTime;
                    if (timer <= 0f) StartWarning();
                    break;

                case LaserPhase.Warning:
                    timer -= Time.fixedDeltaTime;
                    TryPlayAnticipation();
                    if (trackDuringWarning && timer > lockBeforeFire)
                    {
                        AimAtPlayer(Time.fixedDeltaTime);
                        RefreshWarning();
                    }
                    if (timer <= 0f) Fire();
                    break;

                case LaserPhase.Firing:
                    TickFiring();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (phase == LaserPhase.Warning)
            {
                ApplyWarningVisuals();
                return;
            }

            if (phase == LaserPhase.Firing && bodyHideElapsed >= 0f && bodySprite != null && bodySprite.enabled)
            {
                bodyHideElapsed += Time.deltaTime;
                float t = bodyHideFadeDuration > 0f ? Mathf.Clamp01(bodyHideElapsed / bodyHideFadeDuration) : 1f;
                SetBodyAlpha(1f - t);

                if (t >= 1f)
                {
                    bodySprite.enabled = false;
                    bodyHideElapsed = -1f;
                }
            }
        }

        private void ApplyWarningVisuals()
        {
            float progress = warningDelay > 0f ? Mathf.Clamp01(1f - timer / warningDelay) : 1f;
            Color color = Color.Lerp(warningStartColor, warningFireColor, Mathf.Clamp01(warningColorCurve.Evaluate(progress)));
            float width = beamWidth * Mathf.LerpUnclamped(warningStartWidthMultiplier, 1f, Mathf.Clamp01(warningWidthCurve.Evaluate(progress)));

            if (fadeInBodyDuringWarning) SetBodyAlpha(Mathf.Clamp01(bodyFadeCurve.Evaluate(progress)));

            foreach (BeamSegment segment in segments)
            {
                if (segment.Vfx == null) continue;
                if (lerpWarningColor) segment.Vfx.SetWarningColor(color);
                segment.Vfx.SetWarningWidth(width);
            }
        }

        private void TryPlayAnticipation()
        {
            if (!playAnticipation || anticipationPlayed || timer > anticipationLead) return;

            anticipationPlayed = true;
            Events.CurrentAttackForce = beamAttackForce;
            Events.PlayAnticipation();
            Events.HoldCharge(Mathf.Max(0f, timer));
        }

        private void StartWarning()
        {
            phase = LaserPhase.Warning;
            timer = warningDelay;

            Rb.linearVelocity = Vector2.zero;
            HitBox.Disable();

            AimAtPlayer(0f);
            if (startAngleOffset > 0f && beamHitBox != null)
            {
                float offset = Random.value < 0.5f ? -startAngleOffset : startAngleOffset;
                beamHitBox.transform.rotation = Quaternion.Euler(0f, 0f, beamHitBox.transform.eulerAngles.z + offset);
            }
            RefreshWarning();
            ApplyWarningVisuals();

            if (chargeDuringWarning)
            {
                Events.CurrentAttackForce = beamAttackForce;
                Events.StartCharge(warningDelay);
            }

            TryPlayAnticipation();
            AudioManager.PlaySFX(warningEvent, transform.position);

            if (warningDelay <= 0f) Fire();
        }

        private void RefreshWarning()
        {
            ComputePath();
            PlaceSegments();
            SetPathLength(totalPathLength);

            for (int i = 0; i < segments.Count; i++)
            {
                if (i < path.Count) segments[i].Vfx?.ShowWarning(path[i].Length, beamWidth);
                else segments[i].Vfx?.HideWarning();
            }
        }

        private void AimAtPlayer(float deltaTime)
        {
            if (beamHitBox == null) return;

            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;

            Vector2 origin = beamHitBox.transform.position;
            Vector2 toTarget = (Vector2)player.transform.position + targetOffset - origin;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

            if (deltaTime > 0f && aimTurnSpeed > 0f)
                angle = Mathf.MoveTowardsAngle(beamHitBox.transform.eulerAngles.z, angle, aimTurnSpeed * deltaTime);

            beamHitBox.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void ComputePath()
        {
            path.Clear();
            impactPoints.Clear();
            totalPathLength = 0f;

            if (beamHitBox == null) return;

            Vector2 origin = beamHitBox.transform.position;
            Vector2 direction = beamHitBox.transform.right;
            float remaining = beamLength;

            for (int bounce = 0; bounce <= maxBounces && remaining > 0.01f; bounce++)
            {
                RaycastHit2D hit = obstructMask.value != 0
                    ? FindObstruction(origin, direction, remaining)
                    : default;

                if (hit.collider == null)
                {
                    AddPathSegment(origin, direction, remaining);
                    break;
                }

                AddPathSegment(origin, direction, hit.distance);
                impactPoints.Add(hit.point);
                remaining -= hit.distance;

                origin = hit.point + hit.normal * 0.01f;
                direction = Vector2.Reflect(direction, hit.normal).normalized;
            }
        }

        private RaycastHit2D FindObstruction(Vector2 origin, Vector2 direction, float distance)
        {
            obstructionFilter.useTriggers = false;
            obstructionFilter.SetLayerMask(obstructMask);

            int count = Physics2D.Raycast(origin, direction, obstructionFilter, obstructionHits, distance);
            RaycastHit2D closest = default;
            float best = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = obstructionHits[i];
                if (hit.distance >= best || !BlocksLaser(hit.collider)) continue;

                best = hit.distance;
                closest = hit;
            }

            return closest;
        }

        private static bool BlocksLaser(Collider2D col)
        {
            if (col == null) return false;
            if (col.GetComponentInParent<AirOnlyCollisionPlatform>() != null) return false;

            DropThroughPlatform dropThrough = col.GetComponentInParent<DropThroughPlatform>();
            return dropThrough == null || dropThrough.IsClimbable;
        }

        private void AddPathSegment(Vector2 start, Vector2 direction, float length)
        {
            path.Add(new PathSegment { Start = start, Direction = direction, Length = length });
            totalPathLength += length;
        }

        private void PlaceSegments()
        {
            for (int i = 1; i < segments.Count; i++)
            {
                if (i >= path.Count) continue;

                Transform t = segments[i].HitBox.transform;
                t.position = path[i].Start;
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(path[i].Direction.y, path[i].Direction.x) * Mathf.Rad2Deg);
            }
        }

        private void Fire()
        {
            phase = LaserPhase.Firing;
            timer = fireDuration;
            growTimer = 0f;
            hitLanded = false;

            if (segments.Count == 0)
            {
                ReturnToPool();
                return;
            }

            Events.StopCharge();

            ComputePath();
            PlaceSegments();
            SetPathLength(beamGrowDuration > 0f ? 0f : totalPathLength);

            SetBodyAlpha(1f);
            bodyHideElapsed = hideBodyWhenFiring ? 0f : -1f;

            for (int i = 0; i < segments.Count; i++)
            {
                BeamSegment segment = segments[i];
                segment.Vfx?.HideWarning();

                if (i >= path.Count) continue;

                segment.HitBox.Enable(Damage, CombatUtility.GetDirectionFromVelocity(path[i].Direction), beamAttackForce);
                segment.Vfx?.Play();
            }

            if (!string.IsNullOrEmpty(impactEffect))
            {
                foreach (Vector2 point in impactPoints)
                    PSpawner.Spawn(impactEffect, point, Quaternion.identity);
            }

            AudioManager.PlaySFX(fireEvent, transform.position);
        }

        private void TickFiring()
        {
            if (beamGrowDuration > 0f && growTimer < beamGrowDuration)
            {
                growTimer += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(growTimer / beamGrowDuration);
                SetPathLength(totalPathLength * Mathf.Clamp01(beamGrowCurve.Evaluate(t)));
            }

            DamageEnemiesAlongBeam();
            if (!hitLanded) HitPlayersInsideBeam();

            if (!hitLanded)
            {
                foreach (BeamSegment segment in segments)
                {
                    if (!segment.HitBox.HasHit && !segment.HitBox.HasBeenParried) continue;

                    hitLanded = true;
                    foreach (BeamSegment other in segments)
                        if (other != segment) other.HitBox.Disable();
                    break;
                }
            }

            timer -= Time.fixedDeltaTime;
            if (timer <= 0f) ReturnToPool();
        }

        private void HitPlayersInsideBeam()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;
            filter.NoFilter();

            for (int i = 0; i < segments.Count && i < path.Count; i++)
            {
                BeamSegment segment = segments[i];
                if (segment.Collider == null || !segment.Collider.enabled || !segment.HitBox.IsActive) continue;
                if (segment.HitBox.HasHit || segment.HitBox.HasBeenParried) continue;

                int count = segment.Collider.Overlap(filter, playerOverlapHits);
                for (int h = 0; h < count; h++)
                {
                    PlayerHurtBox hurtBox = playerOverlapHits[h].GetComponent<PlayerHurtBox>();
                    if (hurtBox == null || hurtBox.Invulnerable) continue;

                    hurtBox.TakeHit(segment.HitBox, segment.Collider);
                    if (segment.HitBox.HasHit || segment.HitBox.HasBeenParried) break;
                }
            }
        }

        private void DamageEnemiesAlongBeam()
        {
            if (!DamagesEnemies) return;

            for (int i = 0; i < segments.Count && i < path.Count; i++)
            {
                BoxCollider2D col = segments[i].Collider;
                if (col == null) continue;

                float length = col.size.x;
                if (length <= 0.02f) continue;

                Vector2 start = path[i].Start;
                Vector2 direction = path[i].Direction;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                DamageEnemiesInBox(start + direction * (length / 2f), new Vector2(length, BeamHitboxWidth), angle);
            }
        }

        private void SetPathLength(float length)
        {
            float remaining = length;

            for (int i = 0; i < segments.Count; i++)
            {
                float segmentMax = i < path.Count ? path[i].Length : 0f;
                float segmentLength = Mathf.Clamp(remaining, 0f, segmentMax);
                remaining -= segmentLength;

                SetSegmentLength(segments[i], segmentLength, segmentMax);
            }
        }

        private void SetSegmentLength(BeamSegment segment, float length, float maxLength)
        {
            segment.Vfx?.SetLength(length, beamWidth);

            if (segment.Collider == null) return;

            float hitLength = Mathf.Max(0.01f, hitboxGrowsWithBeam ? length : maxLength);
            segment.Collider.size = new Vector2(hitLength, BeamHitboxWidth);
            segment.Collider.offset = new Vector2(hitLength / 2f, 0f);
        }

        public override void OnPoolRelease()
        {
            base.OnPoolRelease();

            phase = LaserPhase.Travel;
            timer = 0f;
            growTimer = 0f;
            hitLanded = false;
            anticipationPlayed = false;
            bodyHideElapsed = -1f;

            if (bodySprite != null) bodySprite.enabled = true;
            SetBodyAlpha(1f);

            ResetSegments();
        }

        private void SetBodyAlpha(float normalized)
        {
            if (bodySprite == null) return;

            Color c = bodySprite.color;
            c.a = (bodyBaseAlpha < 0f ? 1f : bodyBaseAlpha) * normalized;
            bodySprite.color = c;
        }
    }
}