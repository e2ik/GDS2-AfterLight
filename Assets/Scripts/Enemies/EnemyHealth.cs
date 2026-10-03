using System;
using UnityEngine;
using System.Collections;

namespace Enemies
{
    public struct DamageInfo
    {
        public int Amount;
        public EDamageType DamageType;
        public bool IsCrit;
        public bool HasRoll;
        public float RollQuality;
        public ERollTier RollTier;
        public bool IsComboFinisher;
        public Vector3 Position;
    }

    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 10;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        public event Action<int, int, bool> OnDamaged; // amount, currentHealth, isDot
        public event Action<DamageInfo> OnDamageTaken;
        public event Action OnDeath;

        public static event Action<EnemyHealth, DamageInfo> AnyEnemyDamaged;

        private void Awake() => CurrentHealth = maxHealth;
        private Coroutine dotRoutine;

        public void ApplyDamage(int amount, bool isDot = false)
        {
            ApplyDamage(new DamageInfo
            {
                Amount = amount,
                DamageType = isDot ? EDamageType.Dot : EDamageType.Base
            });
        }

        private void ApplyDamage(DamageInfo info)
        {
            if (CurrentHealth <= 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - info.Amount);

            info.Position = transform.position;

            OnDamaged?.Invoke(info.Amount, CurrentHealth, info.DamageType == EDamageType.Dot);
            OnDamageTaken?.Invoke(info);
            AnyEnemyDamaged?.Invoke(this, info);

            if (CurrentHealth == 0)
                OnDeath?.Invoke();
        }

        public void ApplyHit(int damage, AttackContext context, bool isCrit = false)
        {
            ApplyDamage(new DamageInfo
            {
                Amount = damage,
                DamageType = context.DamageType,
                IsCrit = isCrit,
                HasRoll = context.HasRoll,
                RollQuality = context.RollQuality,
                RollTier = context.RollTier,
                IsComboFinisher = context.IsComboFinisher
            });

            if (CurrentHealth <= 0) return;

            if (context.AppliesDot)
            {
                float dotDamagePerTick = damage * context.DotDamagePercent;
                if (dotDamagePerTick < 1f) dotDamagePerTick = 1f; // at least 1 damage

                if (dotRoutine != null)
                    StopCoroutine(dotRoutine);

                dotRoutine = StartCoroutine(DotRoutine(dotDamagePerTick, context.DotTickInterval, context.DotDuration));
            }
        }

        private IEnumerator DotRoutine(float damagePerTick, float tickInterval, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;

                ApplyDamage((int)damagePerTick, isDot: true);
            }

            dotRoutine = null;
        }
    }
}