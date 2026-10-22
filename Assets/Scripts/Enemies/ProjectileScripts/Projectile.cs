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

        [Header("Launch Wall Check")]
        [SerializeField] private bool adjustLaunchForWalls = false;
        [SerializeField] private LayerMask launchWallMask;
        [SerializeField, Min(0f)] private float launchWallCheckDistance = 1.5f;
        [SerializeField, Range(0f, 180f)] private float maxLaunchAdjustAngle = 90f;
        [SerializeField, Range(1f, 45f)] private float launchAdjustStep = 10f;
        [SerializeField] private bool debugLaunchWallCheck = false;
        private readonly RaycastHit2D[] launchCheckHits = new RaycastHit2D[8];

        [Header("Friendly Fire")]
        [SerializeField] private bool damagesEnemies = false;
        [SerializeField] private LayerMask enemyMask;

        [Header("Player Hurt")]
        [SerializeField] private AnimationClip playerHurtClip;
        [SerializeField] private bool overrideHurtFlashColor = false;
        [SerializeField, ColorUsage(false, true)] private Color playerHurtFlashColor = Color.white;

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

            if (adjustLaunchForWalls) initialVelocity = AdjustLaunchForWalls(origin, initialVelocity);

            HitBox.Enable(damage, CombatUtility.GetDirectionFromVelocity(initialVelocity), attackForce);
            ApplyHurtOverride(HitBox);
            Events.OpenParryWindow();

            OnLaunch(initialVelocity);
        }

        protected abstract void OnLaunch(Vector2 initialVelocity);

        private Vector2 AdjustLaunchForWalls(Vector2 origin, Vector2 velocity)
        {
            float speed = velocity.magnitude;
            if (speed < 0.0001f || launchWallMask.value == 0 || launchWallCheckDistance <= 0f) return velocity;

            Vector2 direction = velocity / speed;
            float radius = GetLaunchCheckRadius();

            if (ClearDistance(origin, direction, radius) >= launchWallCheckDistance) return velocity;

            Vector2 bestDirection = direction;
            float bestDistance = ClearDistance(origin, direction, radius);
            int steps = Mathf.FloorToInt(maxLaunchAdjustAngle / launchAdjustStep);

            for (int i = 1; i <= steps; i++)
            {
                float angle = launchAdjustStep * i;
                Vector2 a = Rotate(direction, angle);
                Vector2 b = Rotate(direction, -angle);
                Vector2 first = a.y <= b.y ? a : b;
                Vector2 second = a.y <= b.y ? b : a;

                foreach (Vector2 candidate in new[] { first, second })
                {
                    float distance = ClearDistance(origin, candidate, radius);
                    if (distance >= launchWallCheckDistance)
                    {
                        if (debugLaunchWallCheck)
                            Debug.Log($"[Projectile] {name}: launch blocked, rotated {Vector2.SignedAngle(direction, candidate):F0} degrees", this);
                        return candidate * speed;
                    }

                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        bestDirection = candidate;
                    }
                }
            }

            if (debugLaunchWallCheck)
                Debug.Log($"[Projectile] {name}: no clear launch direction, using the most open one", this);

            return bestDirection * speed;
        }

        private float ClearDistance(Vector2 origin, Vector2 direction, float radius)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = launchWallMask };
            int count = Physics2D.CircleCast(origin, radius, direction, filter, launchCheckHits, launchWallCheckDistance);

            float closest = launchWallCheckDistance;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = launchCheckHits[i].collider;
                if (col == null || col.transform.IsChildOf(transform) || IsLaunchPassThrough(col)) continue;
                if (hasOwner && owner != null && col.transform.IsChildOf(owner)) continue;
                if (launchCheckHits[i].distance <= 0f) continue;
                closest = Mathf.Min(closest, launchCheckHits[i].distance);
            }

            if (debugLaunchWallCheck)
                Debug.DrawRay(origin, direction * closest, closest >= launchWallCheckDistance ? Color.green : Color.red, 1f);

            return closest;
        }

        private float GetLaunchCheckRadius()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == null) return 0.1f;
            Vector3 extents = col.bounds.extents;
            return Mathf.Max(0.01f, Mathf.Min(extents.x, extents.y));
        }

        private static bool IsLaunchPassThrough(Collider2D col)
        {
            return col.usedByEffector
                   || col.GetComponentInParent<PlatformEffector2D>() != null
                   || col.GetComponentInParent<DropThroughPlatform>() != null
                   || col.GetComponentInParent<AirOnlyCollisionPlatform>() != null;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        protected void ApplyHurtOverride(HitBox box)
        {
            if (box != null) box.SetHurtOverride(playerHurtClip, overrideHurtFlashColor, playerHurtFlashColor);
        }

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