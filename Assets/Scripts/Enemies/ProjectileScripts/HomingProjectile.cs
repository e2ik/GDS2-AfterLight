using FMODUnity;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class HomingProjectile : Projectile
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float speed = 6f;
        [SerializeField, Min(0f)] private float turnRate = 180f;
        [SerializeField, Min(0f)] private float homingDelay = 0.15f;
        [SerializeField, Min(0f)] private float homingDuration = 3f;
        [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.5f);
        [SerializeField] private bool rotateToVelocity = true;
        [SerializeField] private bool stopHomingOnMiss = true;
        [SerializeField, Range(0f, 180f)] private float missAngle = 90f;
        [SerializeField, Range(0f, 90f)] private float lockOnAngle = 20f;
        [SerializeField, Min(0f)] private float commitDistance = 2f;

        [Header("Glow Pulse")]
        [SerializeField] private ColorGlow colorGlow;
        [SerializeField] private ParticleGlow[] particleGlows;
        [SerializeField, Range(0f, 10f)] private float pulseMinIntensity = 1f;
        [SerializeField, Range(0f, 10f)] private float pulseMaxIntensity = 3f;
        [SerializeField, Min(0f)] private float pulseSpeed = 2f;
        [SerializeField, ColorUsage(false, true)] private Color homingColor = new Color(1f, 0.3f, 0.2f);
        [SerializeField, ColorUsage(false, true)] private Color homingPulseColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField, ColorUsage(false, true)] private Color notHomingColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField, Range(0f, 10f)] private float notHomingIntensity = 1f;
        [SerializeField, Min(0f)] private float glowBlendSpeed = 12f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color homingSpriteColor = Color.white;
        [SerializeField] private Color notHomingSpriteColor = new Color(0.7f, 0.7f, 0.7f);

        [Header("Collision")]
        [SerializeField] private LayerMask collideWithMask;
        [SerializeField] private EventReference hitEvent;
        [SerializeField] private string hitEffect = "ProjectileExplode";
        [SerializeField, Min(0f)] private float hitAnimationDuration = 0.3f;
        [SerializeField] private string hitTrigger = "Hit";

        private float elapsed;
        private float pulseTime;
        private bool isHoming;
        private Color currentGlowColor;
        private float currentGlowIntensity;
        private Color currentSpriteColor;
        private Color currentParticleColor;
        private bool missed;
        private bool lockedOn;
        private bool hitting;
        private Animator animator;

        protected override void OnLaunch(Vector2 initialVelocity)
        {
            elapsed = 0f;
            missed = false;
            lockedOn = false;
            hitting = false;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            pulseTime = 0f;
            isHoming = false;
            currentGlowColor = notHomingColor;
            currentGlowIntensity = notHomingIntensity;
            currentSpriteColor = notHomingSpriteColor;
            currentParticleColor = notHomingColor;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (colorGlow == null) colorGlow = GetComponentInChildren<ColorGlow>(true);
            if (particleGlows == null || particleGlows.Length == 0) particleGlows = GetComponentsInChildren<ParticleGlow>(true);
            Rb.gravityScale = 0f;

            Vector2 dir = initialVelocity.sqrMagnitude > 0.0001f ? initialVelocity.normalized : DirectionToTarget(Vector2.right);
            Rb.linearVelocity = dir * speed;
            FaceVelocity();
        }

        private void FixedUpdate()
        {
            if (hitting) return;

            if (HitBox.HasBeenParried)
            {
                ReturnToPool();
                return;
            }

            elapsed += Time.fixedDeltaTime;

            Vector2 current = Rb.linearVelocity.sqrMagnitude > 0.0001f ? Rb.linearVelocity.normalized : Vector2.right;
            bool homing = !missed && elapsed >= homingDelay && (homingDuration <= 0f || elapsed <= homingDelay + homingDuration);

            if (homing)
            {
                Vector2 desired = DirectionToTarget(current);

                float angleToTarget = Vector2.Angle(current, desired);

                if (!lockedOn && angleToTarget <= lockOnAngle)
                    lockedOn = true;

                if (stopHomingOnMiss && lockedOn && (angleToTarget > missAngle || DistanceToTarget() <= commitDistance))
                {
                    missed = true;
                    homing = false;
                }
            }

            isHoming = homing;

            if (homing)
            {
                Vector2 desired = DirectionToTarget(current);
                float maxRadians = turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
                current = Vector3.RotateTowards(current, desired, maxRadians, 0f);
            }

            Rb.linearVelocity = current.normalized * speed;
            FaceVelocity();
        }

        private void LateUpdate()
        {
            bool hasParticles = particleGlows != null && particleGlows.Length > 0;
            if (colorGlow == null && !hasParticles && spriteRenderer == null) return;

            Color targetColor = notHomingColor;
            float targetIntensity = notHomingIntensity;

            if (isHoming)
            {
                pulseTime += Time.deltaTime;
                float wave = pulseSpeed > 0f ? (1f - Mathf.Cos(pulseTime * pulseSpeed * Mathf.PI * 2f)) * 0.5f : 1f;
                targetColor = Color.Lerp(homingColor, homingPulseColor, wave);
                targetIntensity = Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, wave);
            }
            else
            {
                pulseTime = 0f;
            }

            float t = glowBlendSpeed <= 0f ? 1f : 1f - Mathf.Exp(-glowBlendSpeed * Time.deltaTime);
            currentGlowColor = Color.Lerp(currentGlowColor, targetColor, t);
            currentGlowIntensity = Mathf.Lerp(currentGlowIntensity, targetIntensity, t);
            currentSpriteColor = Color.Lerp(currentSpriteColor, isHoming ? homingSpriteColor : notHomingSpriteColor, t);
            currentParticleColor = Color.Lerp(currentParticleColor, isHoming ? homingColor : notHomingColor, t);

            if (spriteRenderer != null) spriteRenderer.color = currentSpriteColor;

            if (colorGlow != null)
            {
                colorGlow.SetGlowColor(currentGlowColor);
                colorGlow.SetIntensity(currentGlowIntensity);
            }

            if (!hasParticles) return;

            foreach (ParticleGlow glow in particleGlows)
            {
                if (glow == null) continue;
                glow.SetColor(currentParticleColor);
            }
        }

        private float DistanceToTarget()
        {
            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return float.MaxValue;

            return Vector2.Distance((Vector2)player.transform.position + targetOffset, Rb.position);
        }

        private Vector2 DirectionToTarget(Vector2 fallback)
        {
            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return fallback;

            Vector2 toTarget = (Vector2)player.transform.position + targetOffset - Rb.position;
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : fallback;
        }

        private void FaceVelocity()
        {
            if (!rotateToVelocity || Rb.linearVelocity.sqrMagnitude < 0.0001f) return;

            float angle = Mathf.Atan2(Rb.linearVelocity.y, Rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        protected override bool OnHitTrigger(Collider2D other)
        {
            if (hitting) return false;
            if (other.transform.IsChildOf(transform) || (Owner != null && other.transform.IsChildOf(Owner))) return false;
            if (IsPassThroughPlatform(other)) return false;

            if (TryDamageEnemy(other))
            {
                PlayHitFeedback();
                return StartHit();
            }

            PlayerHurtBox hurtBox = other.GetComponentInParent<PlayerHurtBox>();
            if (hurtBox == null) hurtBox = other.GetComponentInChildren<PlayerHurtBox>();

            if (hurtBox != null)
            {
                Collider2D ownerCollider = Owner != null ? Owner.GetComponentInChildren<Collider2D>() : null;
                hurtBox.TakeHit(HitBox, ownerCollider);
                PlayHitFeedback();
                return StartHit();
            }

            if ((collideWithMask.value & (1 << other.gameObject.layer)) != 0)
            {
                PlayHitFeedback();
                return StartHit();
            }

            return false;
        }

        private static bool IsPassThroughPlatform(Collider2D col)
        {
            return col.GetComponentInParent<DropThroughPlatform>() != null
                   || col.GetComponentInParent<AirOnlyCollisionPlatform>() != null;
        }

        private bool StartHit()
        {
            if (hitAnimationDuration <= 0f) return true;

            hitting = true;
            isHoming = false;
            Rb.linearVelocity = Vector2.zero;
            HitBox.Disable();

            if (animator != null && HasTrigger(hitTrigger)) animator.SetTrigger(hitTrigger);

            Invoke(nameof(ReturnToPool), hitAnimationDuration);
            return false;
        }

        private bool HasTrigger(string trigger)
        {
            if (string.IsNullOrEmpty(trigger) || animator.runtimeAnimatorController == null) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger) return true;

            return false;
        }

        private void PlayHitFeedback()
        {
            if (!string.IsNullOrEmpty(hitEffect)) PSpawner.Spawn(hitEffect, transform.position, Quaternion.identity);
            AudioManager.PlaySFX(hitEvent, transform.position);
        }

        public override void OnPoolRelease()
        {
            base.OnPoolRelease();
            elapsed = 0f;
            missed = false;
            hitting = false;
            lockedOn = false;
            transform.rotation = Quaternion.identity;
        }
    }
}