using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class LobProjectile : Projectile
    {
        [SerializeField] private float gravityScale = 2f;
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private LayerMask playerMask;
        [SerializeField] private LayerMask collideWithMask;

        // guard against multiple hits (player has more than 1 collider)
        private bool hasTriggered = false;
        
        protected override void OnLaunch(Vector2 initialVelocity)
        {
            hasTriggered = false;
            Rb.gravityScale = gravityScale;
            Rb.linearVelocity = initialVelocity;
        }

        protected override bool OnHitTrigger(Collider2D other)
        {
            if (hasTriggered) return false;

            if (other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform))
                return false;

            PlayerHurtBox hurtBox = other.GetComponentInParent<PlayerHurtBox>();
            if (hurtBox != null || ((1 << other.gameObject.layer) & playerMask) != 0)
            {
                if (hurtBox == null)
                    hurtBox = other.GetComponentInChildren<PlayerHurtBox>();

                hasTriggered = true;
                TryExplodeOrParry(hurtBox);
                return true; 
            }

            if ((collideWithMask.value & (1 << other.gameObject.layer)) != 0)
            {
                hasTriggered = true;
                Explode(); 
                return true; 
            }

            return false;
        }

        private void TryExplodeOrParry(PlayerHurtBox directHitBox = null)
        {
            bool wasParried = false;

            if (directHitBox != null)
            {
                if (!directHitBox.TakeHit(HitBox))
                {
                    wasParried = true;
                }
            }

            if (wasParried)
            {
                return; 
            }

            Explode(directHitBox);
        }

        private void Explode(PlayerHurtBox directHitBox = null)
        {
            PSpawner.Spawn("anticipation", transform.position, Quaternion.identity);

            var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, playerMask);
            foreach (var hit in hits)
            {
                PlayerHurtBox hurtBox = hit.GetComponentInParent<PlayerHurtBox>();
                if (hurtBox == null)
                    hurtBox = hit.GetComponentInChildren<PlayerHurtBox>();

                if (hurtBox != null && hurtBox != directHitBox)
                {
                    hurtBox.TakeHit(HitBox);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}