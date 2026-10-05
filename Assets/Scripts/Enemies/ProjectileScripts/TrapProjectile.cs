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

        [Header("Beam Pattern")] 
        [Range(0f, 1f)] [SerializeField] private float diagonalChance = 0.5f;
        [SerializeField] private float beamLength = 6f;
        [SerializeField] private float beamWidth = 0.4f;
        [SerializeField] private float fireDuration = 0.5f;
        [SerializeField] private LayerMask playerMask;
        [SerializeField] private HitBox[] beamHitBoxes = new HitBox[4];

        [Header("Trigger / Warning")] 
        [SerializeField] private float warningDelay = 0.6f;
        [SerializeField] private EventReference warningEvent;
        [SerializeField] private float armedLifetime = 8f;
        
        
        private static readonly float[] CardinalAngles = { 0f, 90f, 180f, 270f };
        private static readonly float[] DiagonalAngles = { 45f, 135f, 225f, 315f };

        private TrapPhase phase;
        private float fireTimer;
        private float warningTimer;
        private float armedTimer;
        private bool wasRisingLastStep;
        
        protected override void OnLaunch(Vector2 initialVelocity)
        {
            phase = TrapPhase.Flying;
            
            Rb.bodyType = RigidbodyType2D.Dynamic;
            Rb.gravityScale = gravityScale;
            Rb.linearVelocity = initialVelocity;
            wasRisingLastStep = initialVelocity.y > 0f;
            
            foreach (HitBox beam in beamHitBoxes)
                beam.Disable();
            
            TrapTracker.Register(this);
        }

        protected override bool OnHitTrigger(Collider2D other) => false;

        private void FixedUpdate()
        {
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
            
            HitBox.Disable();

            AudioManager.PlaySFX(landEvent, transform.position);

            float[] angles = Random.value < diagonalChance ? DiagonalAngles : CardinalAngles;

            for (int i = 0; i < beamHitBoxes.Length; i++)
            {
                Transform beamTransform = beamHitBoxes[i].transform;
                beamTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);

                var collider = beamHitBoxes[i].GetComponent<BoxCollider2D>();
                collider.size = new Vector2(beamLength, beamWidth);
                collider.offset = new Vector2(beamLength / 2f, 0f);
                
                beamHitBoxes[i].GetComponent<LaserBeamVFX>()?.SetLength(beamLength, beamWidth);
            }
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

                if (Physics2D.OverlapBox(center, new Vector2(beamLength, beamWidth), angle, playerMask) != null)
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
            
            foreach (HitBox beam in beamHitBoxes)
                beam.GetComponent<LaserBeamVFX>()?.ShowWarning(beamLength, beamWidth);
        }

        private void TickTriggered()
        {
            warningTimer -= Time.fixedDeltaTime;
            if(warningTimer <= 0f)
                Fire();
        }

        private void Fire()
        {
            phase = TrapPhase.Firing;
            fireTimer = fireDuration;

            foreach (HitBox beam in beamHitBoxes)
            {
                Vector2 direction = beam.transform.right;
                beam.Enable(Damage, CombatUtility.GetDirectionFromVelocity(direction), AttackForce.Heavy);
                var vfx = beam.GetComponent<LaserBeamVFX>();
                vfx?.HideWarning();
                vfx?.Play();
            }
        }

        private void TickFiring()
        {
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