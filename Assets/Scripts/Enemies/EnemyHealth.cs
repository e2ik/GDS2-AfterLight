using System;
using UnityEngine;
using System.Collections;

namespace Enemies
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 10;
        public int CurrentHealth { get; private set; }

        public event Action<int, int, bool> OnDamaged; // amount, currentHealth, isDot
        public event Action OnDeath;

        private void Awake() => CurrentHealth = maxHealth;
        private Coroutine dotRoutine;

        public void ApplyDamage(int amount, bool isDot = false)
        {
            if (CurrentHealth <= 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

            OnDamaged?.Invoke(amount, CurrentHealth, isDot);

            if (CurrentHealth == 0)
                OnDeath?.Invoke();
        }

        public void ApplyHit(int damage, AttackContext context)
        {
            ApplyDamage(damage);

            if (CurrentHealth <= 0) return;

            if (context.AppliesDot)
            {
                float dotDamagePerTick = damage * context.DotDamagePercent;

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