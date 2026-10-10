using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
        public event Action OnHealthDepleted;
        
        public bool DeferDeath { get; set; }
        private bool deathFinalized;

        public static event Action<EnemyHealth, DamageInfo> AnyEnemyDamaged;

        private void Awake() => CurrentHealth = maxHealth;
        private Coroutine dotRoutine;

        private class DotStack
        {
            public float DamagePerTick;
            public int TicksRemaining;
        }

        private readonly List<DotStack> dotStacks = new List<DotStack>();
        private float dotTickInterval = 1f;

        private void OnDisable()
        {
            dotStacks.Clear();
            dotRoutine = null;
        }

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

            int minHealth = Tutorial.TutorialDirector.Instance != null && Tutorial.TutorialDirector.Instance.EnemiesProtected ? 1 : 0;
            CurrentHealth = Mathf.Max(minHealth, CurrentHealth - info.Amount);

            info.Position = transform.position;

            OnDamaged?.Invoke(info.Amount, CurrentHealth, info.DamageType == EDamageType.Dot);
            OnDamageTaken?.Invoke(info);
            AnyEnemyDamaged?.Invoke(this, info);
            RaiseTutorialHitEvents(info);

            if (CurrentHealth == 0)
            {
                OnHealthDepleted?.Invoke();

                if (!DeferDeath)
                    FinalizeDeath();
            }
        }

        public void FinalizeDeath()
        {
            if (deathFinalized)
                return;

            deathFinalized = true;
            
            OnDeath?.Invoke();
            Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyKilled);
        }

        private static void RaiseTutorialHitEvents(DamageInfo info)
        {
            switch (info.DamageType)
            {
                case EDamageType.Base:
                    Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyHit);
                    if (info.IsComboFinisher) Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyComboFinished);
                    if (info.IsCrit) Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyCrit);
                    break;
                case EDamageType.Skill:
                    Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemySkillHit);
                    break;
                case EDamageType.Reflect:
                    Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyReflectHit);
                    break;
            }
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

                AddDotStack(dotDamagePerTick, context.DotTickInterval, context.DotDuration, Mathf.Max(1, context.DotMaxStacks));
            }
        }

        private void AddDotStack(float damagePerTick, float tickInterval, float duration, int maxStacks)
        {
            float interval = Mathf.Max(0.05f, tickInterval);
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration / interval));

            if (maxStacks <= 1) dotStacks.Clear();

            dotStacks.Add(new DotStack { DamagePerTick = damagePerTick, TicksRemaining = ticks });
            while (dotStacks.Count > maxStacks) dotStacks.RemoveAt(0);

            dotTickInterval = interval;
            if (dotRoutine == null) dotRoutine = StartCoroutine(DotRoutine());
        }

        private IEnumerator DotRoutine()
        {
            while (dotStacks.Count > 0)
            {
                yield return new WaitForSeconds(dotTickInterval);

                float total = 0f;
                for (int i = dotStacks.Count - 1; i >= 0; i--)
                {
                    total += dotStacks[i].DamagePerTick;
                    dotStacks[i].TicksRemaining--;
                    if (dotStacks[i].TicksRemaining <= 0) dotStacks.RemoveAt(i);
                }

                if (total > 0f) ApplyDamage((int)total, isDot: true);

                if (CurrentHealth <= 0)
                {
                    dotStacks.Clear();
                    break;
                }
            }

            dotRoutine = null;
        }
    }
}