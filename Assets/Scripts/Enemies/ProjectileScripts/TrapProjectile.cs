using FMODUnity;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class TrapProjectile : Projectile
    {
        private enum TrapPhase {Flying, Armed, Triggered, Firing}
        
        [Header("Flight")]
        [SerializeField] private float gravityScale = 2f;
        [SerializeField] private EventReference landEvent;
        [SerializeField] private bool despawnWhenParried = true;

        [Header("Hover")]
        [SerializeField] private Vector2 hoverAmplitude = new Vector2(0.15f, 0.2f);
        [SerializeField, Min(0f)] private float hoverSpeed = 0.6f;
        [SerializeField, Min(0f)] private float hoverBlendSpeed = 4f;
        [SerializeField] private bool hoverDuringWarning = false;
        [SerializeField] private bool hoverWhileFiring = false;

        [Header("Beam Pattern")] 
        [Range(0f, 1f)] [SerializeField] private float diagonalChance = 0.5f;
        [SerializeField] private float beamLength = 6f;
        [SerializeField] private float beamWidth = 0.4f;
        [SerializeField, Min(0f)] private float beamHitboxExtraWidth = 0.1f;
        [SerializeField] private float fireDuration = 0.5f;
        [SerializeField, Min(0f)] private float beamGrowDuration = 0.1f;
        [SerializeField] private AnimationCurve beamGrowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool hitboxGrowsWithBeam = true;
        [SerializeField] private LayerMask playerMask;
        [SerializeField] private HitBox[] beamHitBoxes = new HitBox[4];
        [SerializeField] private AttackForce beamAttackForce = AttackForce.Heavy;
        [SerializeField] private bool beamsHitOnce = true;

        [Header("Detonation")]
        [SerializeField] private SpriteRenderer trapSprite;
        [SerializeField] private EventReference explodeEvent;

        [Header("Trigger / Warning")] 
        [SerializeField] private float warningDelay = 0.6f;
        [SerializeField] private EventReference warningEvent;
        [SerializeField] private float armedLifetime = 8f;

        [Header("Armed Glow")]
        [SerializeField] private HitFlash armedGlow;
        [SerializeField] private ColorGlow colorGlow;
        [SerializeField] private HitFlash.FlashMode armedGlowMode = HitFlash.FlashMode.Edge;
        [SerializeField] private bool edgeGlowInFlight = true;
        [SerializeField, ColorUsage(false, true)] private Color flightGlowColor = new Color(1f, 0.7f, 0f);
        [SerializeField, Min(0f)] private float flightGlowIntensity = 2f;
        [SerializeField, Min(0f)] private float flightPulseSpeed = 0f;
        [SerializeField, Min(0f)] private float glowTransitionSpeed = 10f;
        [SerializeField, ColorUsage(false, true)] private Color armedGlowColor = Color.cyan;
        [SerializeField, Min(0f)] private float armedGlowIntensity = 2f;
        [SerializeField, Min(0f)] private float armedPulseSpeed = 1f;
        [SerializeField, ColorUsage(false, true)] private Color warningGlowColor = Color.red;
        [SerializeField, Min(0f)] private float warningGlowIntensity = 3f;
        [SerializeField, Min(0f)] private float warningPulseSpeed = 6f;
        
        
        private static readonly float[] CardinalAngles = { 0f, 90f, 180f, 270f };
        private static readonly float[] DiagonalAngles = { 45f, 135f, 225f, 315f };

        private TrapPhase phase;
        private float fireTimer;
        private float warningTimer;
        private float armedTimer;
        private bool wasRisingLastStep;
        private bool disableBodyHitPending;
        private Color glowTargetColor;
        private float glowTargetIntensity;
        private float glowPulseSpeed;
        private bool glowEdgeOn;
        private Color glowCurrentColor;
        private float glowCurrentIntensity;
        private bool glowActive;
        private bool beamDisablePending;
        private bool beamsSpent;
        private float beamGrowTimer;
        private float currentBeamLength;
        private Vector2 hoverAnchor;
        private Vector2 hoverSeed;
        private float hoverTime;
        private float hoverWeight;

        private float BeamHitboxWidth => beamWidth + beamHitboxExtraWidth;
        
        protected override void OnLaunch(Vector2 initialVelocity)
        {
            phase = TrapPhase.Flying;
            
            Rb.bodyType = RigidbodyType2D.Dynamic;
            Rb.gravityScale = gravityScale;
            Rb.linearVelocity = initialVelocity;
            wasRisingLastStep = initialVelocity.y > 0f;
            disableBodyHitPending = false;
            
            foreach (HitBox beam in beamHitBoxes)
                beam.Disable();

            if (trapSprite == null) trapSprite = GetComponent<SpriteRenderer>();
            if (trapSprite != null) trapSprite.enabled = true;

            if (armedGlow == null) armedGlow = GetComponentInChildren<HitFlash>(true);
            if (colorGlow == null) colorGlow = GetComponentInChildren<ColorGlow>(true);
            if (armedGlow != null) armedGlow.ReleaseHold(true);

            ApplyGlow(flightGlowColor, flightGlowIntensity, flightPulseSpeed, edgeGlowInFlight, true);
            
            TrapTracker.Register(this);
        }

        protected override bool OnHitTrigger(Collider2D other)
        {
            if (phase != TrapPhase.Flying) return false;

            PlayerHurtBox hurtBox = other.GetComponent<PlayerHurtBox>();
            if (hurtBox != null && !hurtBox.Invulnerable) disableBodyHitPending = true;

            return false;
        }

        private void FixedUpdate()
        {
            if (disableBodyHitPending)
            {
                disableBodyHitPending = false;
                HitBox.Disable();
            }

            if (despawnWhenParried && phase == TrapPhase.Flying && HitBox.HasBeenParried)
            {
                ReturnToPool();
                return;
            }

            TickHover();

            switch (phase)
            {
                case TrapPhase.Flying:
                    TickFlying();
                    break;
                case TrapPhase.Armed:
                    TickArmed();
                    break;
                case TrapPhase.Triggered:
                    TickTriggered();
                    break;
                case TrapPhase.Firing:
                    TickFiring();
                    break;
            }
        }


        private void TickFlying()
        {
            bool isRisingNow = Rb.linearVelocity.y > 0f;
            if(wasRisingLastStep && !isRisingNow)
                Freeze();

            wasRisingLastStep = isRisingNow;
        }
        private void Freeze()
        {
            phase = TrapPhase.Armed;
            armedTimer = armedLifetime;
            
            Rb.linearVelocity = Vector2.zero;
            Rb.gravityScale = 0f;
            Rb.bodyType = RigidbodyType2D.Kinematic;

            hoverAnchor = Rb.position;
            hoverSeed = new Vector2(Random.Range(0f, 100f), Random.Range(0f, 100f));
            hoverTime = 0f;
            hoverWeight = 0f;
            
            HitBox.Disable();

            AudioManager.PlaySFX(landEvent, transform.position);

            ApplyGlow(armedGlowColor, armedGlowIntensity, armedPulseSpeed, true);

            float[] angles = Random.value < diagonalChance ? DiagonalAngles : CardinalAngles;

            for (int i = 0; i < beamHitBoxes.Length; i++)
            {
                Transform beamTransform = beamHitBoxes[i].transform;
                beamTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);

                var collider = beamHitBoxes[i].GetComponent<BoxCollider2D>();
                collider.size = new Vector2(beamLength, BeamHitboxWidth);
                collider.offset = new Vector2(beamLength / 2f, 0f);
                
                beamHitBoxes[i].GetComponent<LaserBeamVFX>()?.SetLength(beamLength, beamWidth);
            }
        }
        
        private void TickHover()
        {
            if (phase == TrapPhase.Flying) return;

            bool shouldHover = phase == TrapPhase.Armed
                               || (phase == TrapPhase.Triggered && hoverDuringWarning)
                               || (phase == TrapPhase.Firing && hoverWhileFiring);

            float dt = Time.fixedDeltaTime;
            float blend = hoverBlendSpeed <= 0f ? 1f : 1f - Mathf.Exp(-hoverBlendSpeed * dt);
            hoverWeight = Mathf.Lerp(hoverWeight, shouldHover ? 1f : 0f, blend);
            hoverTime += dt * hoverSpeed;

            float x = (Mathf.PerlinNoise(hoverSeed.x, hoverTime) - 0.5f) * 2f * hoverAmplitude.x;
            float y = (Mathf.PerlinNoise(hoverSeed.y, hoverTime + 37.1f) - 0.5f) * 2f * hoverAmplitude.y;

            Rb.MovePosition(hoverAnchor + new Vector2(x, y) * hoverWeight);
        }

        private void TickArmed()
        {
            armedTimer -= Time.fixedDeltaTime;
            if (armedTimer <= 0f)
            {
                TriggerWarning();
                return;
            }
            
            foreach (HitBox beam in beamHitBoxes)
            {
                Vector2 direction = beam.transform.right;
                Vector2 center = (Vector2)beam.transform.position + direction * (beamLength / 2f);
                float angle = beam.transform.eulerAngles.z;

                if (Physics2D.OverlapBox(center, new Vector2(beamLength, BeamHitboxWidth), angle, playerMask) != null)
                {
                    TriggerWarning();
                    return;
                }
            }
        }

        private void TriggerWarning()
        {
            phase = TrapPhase.Triggered;
            warningTimer = warningDelay;
            
            AudioManager.PlaySFX(warningEvent, transform.position);

            ApplyGlow(warningGlowColor, warningGlowIntensity, warningPulseSpeed, true);
            
            foreach (HitBox beam in beamHitBoxes)
                beam.GetComponent<LaserBeamVFX>()?.ShowWarning(beamLength, beamWidth);
        }

        private void SetBeamLength(float length)
        {
            currentBeamLength = length;

            foreach (HitBox beam in beamHitBoxes)
            {
                beam.GetComponent<LaserBeamVFX>()?.SetLength(length, beamWidth);

                if (!hitboxGrowsWithBeam) continue;

                var collider = beam.GetComponent<BoxCollider2D>();
                if (collider == null) continue;

                float hitLength = Mathf.Max(0.01f, length);
                collider.size = new Vector2(hitLength, BeamHitboxWidth);
                collider.offset = new Vector2(hitLength / 2f, 0f);
            }
        }

        private bool PlayerTouchingBeam()
        {
            foreach (HitBox beam in beamHitBoxes)
            {
                Vector2 direction = beam.transform.right;
                float length = hitboxGrowsWithBeam ? currentBeamLength : beamLength;
                if (length <= 0.01f) continue;

                Vector2 center = (Vector2)beam.transform.position + direction * (length / 2f);
                float angle = beam.transform.eulerAngles.z;

                Collider2D hit = Physics2D.OverlapBox(center, new Vector2(length, BeamHitboxWidth), angle, playerMask);
                if (hit == null) continue;

                PlayerHurtBox hurtBox = hit.GetComponentInParent<Player>()?.GetComponentInChildren<PlayerHurtBox>();
                if (hurtBox == null || !hurtBox.Invulnerable) return true;
            }

            return false;
        }

        private void ApplyGlow(Color color, float intensity, float pulseSpeed, bool edgeOn, bool snap = false)
        {
            glowTargetColor = color;
            glowTargetIntensity = intensity;
            glowPulseSpeed = pulseSpeed;
            glowEdgeOn = edgeOn;
            glowActive = true;

            if (snap)
            {
                glowCurrentColor = color;
                glowCurrentIntensity = intensity;
            }

            PushGlow();
        }

        private void LateUpdate()
        {
            if (!glowActive) return;

            float t = glowTransitionSpeed <= 0f ? 1f : 1f - Mathf.Exp(-glowTransitionSpeed * Time.deltaTime);
            glowCurrentColor = Color.Lerp(glowCurrentColor, glowTargetColor, t);
            glowCurrentIntensity = Mathf.Lerp(glowCurrentIntensity, glowTargetIntensity, t);

            PushGlow();
        }

        private void PushGlow()
        {
            if (armedGlow != null)
            {
                if (glowEdgeOn) armedGlow.Hold(glowCurrentColor, glowCurrentIntensity, armedGlowMode, glowPulseSpeed);
                else armedGlow.ReleaseHold();
            }

            if (colorGlow != null)
            {
                colorGlow.SetGlowColor(glowCurrentColor);
                colorGlow.GlowOn();
            }
        }

        private void TickTriggered()
        {
            warningTimer -= Time.fixedDeltaTime;
            if(warningTimer <= 0f)
                Fire();         
        }

        private void Fire()
        {
            AudioManager.PlaySFX(explodeEvent);
            phase = TrapPhase.Firing;
            fireTimer = fireDuration;
            beamDisablePending = false;
            beamsSpent = false;
            beamGrowTimer = 0f;
            SetBeamLength(beamGrowDuration > 0f ? 0f : beamLength);
            AudioManager.PlaySFX(explodeEvent, transform.position);
            if (trapSprite != null) trapSprite.enabled = false;
            if (armedGlow != null) armedGlow.ReleaseHold(true);
            glowActive = false;
            foreach (HitBox beam in beamHitBoxes)
            {
                Vector2 direction = beam.transform.right;
                beam.Enable(Damage, CombatUtility.GetDirectionFromVelocity(direction), beamAttackForce);
                var vfx = beam.GetComponent<LaserBeamVFX>();
                vfx?.HideWarning();
                vfx?.Play();
            }
        }

        private void TickFiring()
        {
            if (beamGrowDuration > 0f && beamGrowTimer < beamGrowDuration)
            {
                beamGrowTimer += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(beamGrowTimer / beamGrowDuration);
                SetBeamLength(beamLength * Mathf.Clamp01(beamGrowCurve.Evaluate(t)));
            }

            if (beamsHitOnce && !beamsSpent)
            {
                if (beamDisablePending)
                {
                    beamsSpent = true;
                    foreach (HitBox beam in beamHitBoxes)
                        beam.Disable();
                }
                else if (PlayerTouchingBeam())
                {
                    beamDisablePending = true;
                }
            }

            fireTimer -= Time.fixedDeltaTime;
            if(fireTimer <= 0f)
                ReturnToPool();
        }

        public override void OnPoolRelease()
        {
            base.OnPoolRelease();
            
            TrapTracker.Unregister(this);
            Rb.bodyType = RigidbodyType2D.Dynamic;
            phase = TrapPhase.Flying;
            disableBodyHitPending = false;
            beamDisablePending = false;
            beamsSpent = false;
            hoverWeight = 0f;
            if (trapSprite != null) trapSprite.enabled = true;
            if (armedGlow != null) armedGlow.ReleaseHold(true);
            glowActive = false;
            fireTimer = 0f;
            warningTimer = 0f;
            armedTimer = 0f;

            foreach (HitBox beam in beamHitBoxes)
            {
                beam.Disable();
                var vfx = beam.GetComponent<LaserBeamVFX>();
                vfx?.Stop();
                vfx?.HideWarning();
            }
        }
    }
}