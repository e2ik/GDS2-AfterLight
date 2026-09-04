using System;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HitBox))]
    [RequireComponent(typeof(AttackEvents))]
    public abstract class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = float.MaxValue;
        [SerializeField] private AttackForce attackForce;
        
        protected Rigidbody2D Rb { get; private set; }
        protected HitBox HitBox { get; private set; }
        protected AttackEvents Events { get; private set; }
        protected int Damage { get; private set; }
        
        public Projectile SourcePrefab { get; set; }

        private float _lifeTimer;

        protected void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            HitBox = GetComponent<HitBox>();
            Events = GetComponent<AttackEvents>();
        }

        public void Launch(Vector2 origin, Vector2 initialVelocity, int damage)
        {
            transform.position = origin;
            Damage = damage;
            _lifeTimer = lifetime;
            
            HitBox.Enable(damage, CombatUtility.GetDirectionFromVelocity(initialVelocity), attackForce);
            Events.OpenParryWindow();

            OnLaunch(initialVelocity);
        }

        protected abstract void OnLaunch(Vector2 initialVelocity);

        protected void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                //ReturnToPool();
                return;
            }

            if (Rb.linearVelocity.sqrMagnitude > 0.0001f)
                HitBox.UpdateParryDirection(CombatUtility.GetDirectionFromVelocity(Rb.linearVelocity));
        }

        protected void ReturnToPool() => ProjectilePool.Release(SourcePrefab, this);
        
        protected virtual bool OnHitTrigger(Collider2D other) => true;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if(OnHitTrigger(other))
                ReturnToPool();
        }
        
        public virtual void OnPoolRelease()
        {
            Rb.linearVelocity = Vector2.zero;
            HitBox.Disable();
            Events.CloseParryWindow();
        }
    }
}
