using System.Linq.Expressions;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class LobProjectile : Projectile
    {
        [SerializeField] private float gravityScale = 2f;
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private LayerMask hurtboxMask;
        [SerializeField] private LayerMask explodeIngoreMask;
        
        protected override void OnLaunch(Vector2 initialVelocity)
        {
            Rb.gravityScale = gravityScale;
            Rb.linearVelocity = initialVelocity;
        }

        protected override bool OnHitTrigger(Collider2D other)
        {
            if ((explodeIngoreMask.value & (1 << other.gameObject.layer)) != 0)
                return false;
            
            Explode();
            return true;
        }

        private void Explode()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, hurtboxMask);

            foreach (var hit in hits)
            {
                if(hit.TryGetComponent(out PlayerHurtBox hurtBox))
                    hurtBox.TakeHit(HitBox);
            }
        }
    }
}
