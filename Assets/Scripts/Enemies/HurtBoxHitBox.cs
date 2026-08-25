using UnityEngine;

namespace Enemies
{
    
    [RequireComponent(typeof(Collider2D))]
    public class HurtBox : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        public bool Invulnerable;
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Invulnerable) return;
 
            var hitbox = other.GetComponent<HitBox>();
            if (hitbox != null && hitbox.IsActive)
                health.ApplyDamage(hitbox.Damage);
        }
    }

    [RequireComponent(typeof(Collider2D))]
    public class HitBox : MonoBehaviour
    {
        [SerializeField] private Collider2D col;
        public int Damage { get; private set; }
        public bool IsActive => col.enabled;

        public void Activate(int damage)
        {
            Damage = damage;
            col.enabled = true;
        }

        public void Deactivate() => col.enabled = false;
    }
}