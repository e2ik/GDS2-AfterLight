using System.Linq.Expressions;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class LobProjectile : Projectile
    {
        [SerializeField] private float gravityScale = 2f;
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private LayerMask hurtboxMask;
        [SerializeField] private LayerMask explodeIgnoreMask;
        [SerializeField] private LayerMask collideWithMask;
        
        protected override void OnLaunch(Vector2 initialVelocity)
        {
            Rb.gravityScale = gravityScale;
            Rb.linearVelocity = initialVelocity;
        }

        private void FixedUpdate()
        {
            Collider2D hitHurtbox = Physics2D.OverlapCircle(transform.position, 0.2f, hurtboxMask);
            if (hitHurtbox != null && hitHurtbox.TryGetComponent<PlayerHurtBox>(out var hurtBox))
            {
                Explode();
            }
        }

        protected override bool OnHitTrigger(Collider2D other)
        {
            // ignore self
            if (other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform))
                return false;

            // ignored layers
            if ((explodeIgnoreMask.value & (1 << other.gameObject.layer)) != 0)
                return false;

            if ((collideWithMask.value & (1 << other.gameObject.layer)) == 0)
                return false;

            // player filtering
            bool isPlayer = other.GetComponentInParent<PlayerHurtBox>() != null;
            if (isPlayer)
            {
                if (other.TryGetComponent<PlayerHurtBox>(out var hurtBox))
                {
                    Explode();
                    return true;
                }

                Collider2D nearbyHurtbox = Physics2D.OverlapCircle(transform.position, 0.5f, hurtboxMask);
                if (nearbyHurtbox != null)
                {
                    Explode();
                    return true;
                }

                return false;
            }

            Explode();
            return true;
        }

        private void Explode()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, hurtboxMask);

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out PlayerHurtBox hurtBox))
                    hurtBox.TakeHit(HitBox);
            }
            // prevent multiple explosions
            gameObject.SetActive(false);
        }
    }
}