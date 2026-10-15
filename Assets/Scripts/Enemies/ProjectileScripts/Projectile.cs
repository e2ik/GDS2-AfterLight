using System;
using System.Collections.Generic;
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

        [Header("Friendly Fire")]
        [SerializeField] private bool damagesEnemies = false;
        [SerializeField] private LayerMask enemyMask;

        protected Rigidbody2D Rb { get; private set; }
        protected HitBox HitBox { get; private set; }
        protected AttackEvents Events { get; private set; }
        protected int Damage { get; private set; }

        public Projectile SourcePrefab { get; set; }
        public Transform Owner => owner;
        public virtual bool HandlesOwnAnticipation => false;

        private Transform owner;
        private bool hasOwner;
        private bool isReleased;

        private float _lifeTimer;
        private readonly HashSet<EnemyHealth> enemiesHit = new HashSet<EnemyHealth>();
        private readonly List<Collider2D> enemyOverlapResults = new List<Collider2D>();

        protected bool DamagesEnemies => damagesEnemies;

        protected void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            HitBox = GetComponent<HitBox>();
            Events = GetComponent<AttackEvents>();
        }

        public void Launch(Vector2 origin, Vector2 initialVelocity, int damage, Transform owner = null)
        {
            CancelInvoke();
            isReleased = false;

            transform.position = origin;
            Damage = damage;
            _lifeTimer = lifetime;

            this.owner = owner;
            hasOwner = owner != null;
            enemiesHit.Clear();

            HitBox.Enable(damage, CombatUtility.GetDirectionFromVelocity(initialVelocity), attackForce);
            Events.OpenParryWindow();

            OnLaunch(initialVelocity);
        }

        protected abstract void OnLaunch(Vector2 initialVelocity);

        protected void Update()
        {
            if (isReleased) return;

            if (hasOwner && (owner == null || !owner.gameObject.activeInHierarchy))
            {
                ReturnToPool();
                return;
            }

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                ReturnToPool();
                return;
            }

            if (Rb.linearVelocity.sqrMagnitude > 0.0001f)
                HitBox.UpdateParryDirection(CombatUtility.GetDirectionFromVelocity(Rb.linearVelocity));
        }

        protected void ReturnToPool()
        {
            if (isReleased) return;
            isReleased = true;

            ProjectilePool.Release(SourcePrefab, this);
        }

        protected bool IsEnemyTarget(Collider2D other, out EnemyHealth enemy)
        {
            enemy = null;
            if (!damagesEnemies || other == null || !other.CompareTag("EnemyHurtBox")) return false;

            enemy = other.GetComponentInParent<EnemyHealth>();
            if (enemy == null) return false;

            if (owner != null && (enemy.transform == owner || enemy.transform.IsChildOf(owner) || owner.IsChildOf(enemy.transform)))
                return false;

            return true;
        }

        protected bool TryDamageEnemy(Collider2D other)
        {
            if (!IsEnemyTarget(other, out EnemyHealth enemy)) return false;
            if (!enemiesHit.Add(enemy)) return false;

            enemy.ApplyDamage(Damage);
            return true;
        }

        protected void DamageEnemiesInCircle(Vector2 center, float radius)
        {
            if (!damagesEnemies) return;

            Physics2D.OverlapCircle(center, radius, EnemyFilter(), enemyOverlapResults);
            foreach (Collider2D hit in enemyOverlapResults)
                TryDamageEnemy(hit);
        }

        protected void DamageEnemiesInBox(Vector2 center, Vector2 size, float angle)
        {
            if (!damagesEnemies) return;

            Physics2D.OverlapBox(center, size, angle, EnemyFilter(), enemyOverlapResults);
            foreach (Collider2D hit in enemyOverlapResults)
                TryDamageEnemy(hit);
        }

        private ContactFilter2D EnemyFilter()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(enemyMask);
            filter.useTriggers = true;
            return filter;
        }

        protected void ClearEnemiesHit() => enemiesHit.Clear();

        protected virtual bool OnHitTrigger(Collider2D other) => true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isReleased) return;
            if (other.GetComponentInParent<AirOnlyCollisionPlatform>() != null) return;

            if(OnHitTrigger(other))
                ReturnToPool();
        }

        public virtual void OnPoolRelease()
        {
            CancelInvoke();
            Rb.linearVelocity = Vector2.zero;
            HitBox.Disable();
            Events.CloseParryWindow();
        }
    }
}