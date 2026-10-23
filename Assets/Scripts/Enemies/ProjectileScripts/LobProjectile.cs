using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public class LobProjectile : Projectile
    {
        [SerializeField] private float gravityScale = 2f;
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private LayerMask playerMask;
        [SerializeField] private LayerMask collideWithMask;
        [SerializeField] private EventReference explodeEvent;

        [SerializeField] private float hitAnimationDuration = 0.3f;

        [Header("Repeat Hit Reduction")]
        [SerializeField, Min(0f)] private float repeatHitWindow = 0.5f;
        [SerializeField, Range(0f, 1f)] private float repeatHitDamageMultiplier = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minimumRepeatMultiplier = 0.1f;
        private static float lastPlayerHitTime = float.NegativeInfinity;
        private static int repeatStreak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRepeatHits()
        {
            lastPlayerHitTime = float.NegativeInfinity;
            repeatStreak = 0;
        }
        private Animator animator;

        // guard against multiple hits (player has more than 1 collider)
        private bool hasTriggered = false;
        private readonly HashSet<PlayerHurtBox> explosionHits = new HashSet<PlayerHurtBox>();

        protected override void OnLaunch(Vector2 initialVelocity)
        {
            hasTriggered = false;
            animator = GetComponent<Animator>();

            Rb.gravityScale = gravityScale;
            Rb.linearVelocity = initialVelocity;
        }

        protected override bool OnHitTrigger(Collider2D other)
        {
            if (hasTriggered) return false;

            if (other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform))
                return false;

            if (IsPassThroughPlatform(other))
                return false;

            PlayerHurtBox hurtBox = other.GetComponentInParent<PlayerHurtBox>();
            if (hurtBox != null || ((1 << other.gameObject.layer) & playerMask) != 0)
            {
                if (hurtBox == null)
                    hurtBox = other.GetComponentInChildren<PlayerHurtBox>();

                hasTriggered = true;
                TryExplodeOrParry(hurtBox);
                PlayHitAnimation();
                return false;
            }

            if (IsEnemyTarget(other, out _))
            {
                hasTriggered = true;
                TryDamageEnemy(other);
                Explode();
                PlayHitAnimation();
                return false;
            }

            if ((collideWithMask.value & (1 << other.gameObject.layer)) != 0)
            {
                hasTriggered = true;
                Explode();
                PlayHitAnimation();
                return false;
            }

            return false;
        }

        private static bool IsPassThroughPlatform(Collider2D col)
        {
            return col.GetComponentInParent<DropThroughPlatform>() != null
                   || col.GetComponentInParent<AirOnlyCollisionPlatform>() != null;
        }

        private void TryExplodeOrParry(PlayerHurtBox directHitBox = null)
        {
            if (directHitBox != null)
            {
                Collider2D ownerCollider = Owner != null ? Owner.GetComponentInChildren<Collider2D>() : null;
                int streak = PrepareRepeatHitDamage();
                if (directHitBox.TakeHit(HitBox, ownerCollider)) RecordPlayerHit(streak);
            }

            if (HitBox.HasBeenParried)
            {
                return;
            }

            Explode(directHitBox);
        }

        private void Explode(PlayerHurtBox directHitBox = null)
        {
            PSpawner.Spawn("ProjectileExplode", transform.position, Quaternion.identity);

            AudioManager.PlaySFX(explodeEvent, transform.position);

            explosionHits.Clear();
            if (directHitBox != null) explosionHits.Add(directHitBox);

            var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, playerMask);
            foreach (var hit in hits)
            {
                PlayerHurtBox hurtBox = hit.GetComponentInParent<PlayerHurtBox>();
                if (hurtBox == null)
                    hurtBox = hit.GetComponentInChildren<PlayerHurtBox>();

                if (hurtBox != null && explosionHits.Add(hurtBox))
                {
                    int streak = PrepareRepeatHitDamage();
                    if (hurtBox.TakeHit(HitBox)) RecordPlayerHit(streak);
                }
            }

            DamageEnemiesInCircle(transform.position, explosionRadius);
        }

        private int PrepareRepeatHitDamage()
        {
            bool repeat = repeatHitWindow > 0f && Time.time - lastPlayerHitTime < repeatHitWindow;
            int streak = repeat ? repeatStreak + 1 : 0;

            float multiplier = streak > 0 ? Mathf.Max(minimumRepeatMultiplier, Mathf.Pow(repeatHitDamageMultiplier, streak)) : 1f;
            HitBox.SetDamage(streak > 0 ? Mathf.Max(1, Mathf.RoundToInt(Damage * multiplier)) : Damage);
            return streak;
        }

        private static void RecordPlayerHit(int streak)
        {
            lastPlayerHitTime = Time.time;
            repeatStreak = streak;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

        private void PlayHitAnimation()
        {
            Rb.linearVelocity = Vector2.zero;
            Rb.gravityScale = 0f;
            HitBox.Disable();

            animator.SetTrigger("Hit");

            Invoke(nameof(ReturnToPool), hitAnimationDuration);
        }
    }
}