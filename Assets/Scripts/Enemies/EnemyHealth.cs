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
        public int Blocked;
        public bool IsBonus;
        public int Resisted;
    }

    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private bool allowHealthScaling = false;
        private int scaledMaxHealth;
        private UnityEngine.SceneManagement.Scene originScene;
        private bool hasOriginScene;

        public void SetOriginScene(UnityEngine.SceneManagement.Scene scene)
        {
            originScene = scene;
            hasOriginScene = true;
        }
        [SerializeField] private bool blockDamageOutsideBonus = false;
        [SerializeField, Min(0)] private int blockedHitDamage = 1;

        [Header("Damage Cap")]
        [SerializeField] private bool useDamageCap = false;
        [SerializeField, Range(0.01f, 1f)] private float thresholdPercent = 0.2f;
        [SerializeField, Range(0f, 1f)] private float excessEfficiency = 0.4f;
        [SerializeField, Range(0f, 2f)] private float kneeWidth = 0.5f;
        [SerializeField] private bool useHitCap = false;
        [SerializeField, Range(0.01f, 1f)] private float maxHitPercent = 0.35f;
        [SerializeField, Range(0f, 1f)] private float hitCapSoftStart = 0.7f;
        [SerializeField, Range(0f, 0.25f)] private float cappedVariance = 0.05f;
        [SerializeField, Range(0f, 1f)] private float minResistedToShow = 0.15f;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => scaledMaxHealth > 0 ? scaledMaxHealth : maxHealth;

        public event Action<int, int, bool> OnDamaged; // amount, currentHealth, isDot
        public event Action<DamageInfo> OnDamageTaken;
        public event Action OnDeath;
        public event Action OnHealthDepleted;
        
        public bool DeferDeath { get; set; }
        public bool Invulnerable { get; set; }
        private bool deathFinalized;

        public static event Action<EnemyHealth, DamageInfo> AnyEnemyDamaged;

        private AttackEvents attackEvents;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            scaledMaxHealth = maxHealth;
            attackEvents = GetComponentInChildren<AttackEvents>(true);
        }
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

        public UnityEngine.SceneManagement.Scene ScalingScene => hasOriginScene ? originScene : gameObject.scene;

        public void ApplyHealthScaling(float multiplier)
        {
            if (!allowHealthScaling || CurrentHealth <= 0) return;

            int target = Mathf.Max(1, Mathf.RoundToInt(maxHealth * Mathf.Max(1f, multiplier)));
            int current = MaxHealth;
            if (target <= current) return;

            float ratio = current > 0 ? (float)CurrentHealth / current : 1f;
            scaledMaxHealth = target;
            CurrentHealth = Mathf.Clamp(Mathf.RoundToInt(target * ratio), 1, target);
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
            if (CurrentHealth <= 0 || Invulnerable) return;

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

        public void ApplyHit(int damage, AttackContext context, bool isCrit = false, int dotSourceDamage = -1)
        {
            if (Invulnerable) return;

            float dotRatio = dotSourceDamage >= 0 && damage > 0 ? Mathf.Clamp01((float)dotSourceDamage / damage) : 1f;

            int blocked = 0;
            bool oneHitKill = GameManager.Instance != null && GameManager.Instance.OneHitKillEnemies;
            bool bonus = !oneHitKill && attackEvents != null && attackEvents.IsPlayerBonusActive();

            if (oneHitKill)
            {
                damage = Mathf.Max(damage, CurrentHealth);
            }
            else if (bonus)
            {
                damage = Mathf.RoundToInt(damage * attackEvents.BonusDamageMultiplier);
            }
            else if (blockDamageOutsideBonus && damage > blockedHitDamage)
            {
                blocked = damage - blockedHitDamage;
                damage = blockedHitDamage;
            }

            int resisted = 0;
            if (!oneHitKill && !bonus && blocked == 0 && context.DamageType != EDamageType.Dot)
            {
                int capped = ApplyDamageCap(damage);
                resisted = damage - capped;
                if (resisted < damage * minResistedToShow) resisted = 0;
                damage = capped;
            }

            ApplyDamage(new DamageInfo
            {
                Amount = damage,
                Blocked = blocked,
                IsBonus = bonus,
                Resisted = resisted,
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
                float dotDamagePerTick = damage * dotRatio * context.DotDamagePercent;
                if (dotDamagePerTick < 1f) dotDamagePerTick = 1f; // at least 1 damage

                AddDotStack(dotDamagePerTick, context.DotTickInterval, context.DotDuration, Mathf.Max(1, context.DotMaxStacks));
            }
        }

        private void Start()
        {
            if (!allowHealthScaling) return;

            ApplyHealthScaling(EnemyHealthScalingZone.GetMultiplier(ScalingScene));
        }

        private int ApplyDamageCap(int damage)
        {
            if (damage <= 1 || (!useDamageCap && !useHitCap)) return damage;

            float raw = damage;
            float result = raw;

            if (useDamageCap) result = SoftKnee(result, MaxHealth * thresholdPercent);
            if (useHitCap) result = SoftCeiling(result, Mathf.Max(1f, MaxHealth * maxHitPercent));

            if (result < raw && cappedVariance > 0f)
                result *= UnityEngine.Random.Range(1f - cappedVariance, 1f + cappedVariance);

            return Mathf.Clamp(Mathf.RoundToInt(result), 1, damage);
        }

        private float SoftKnee(float x, float threshold)
        {
            float width = threshold * kneeWidth;
            float kneeStart = threshold - width * 0.5f;

            if (x <= kneeStart) return x;

            if (width > 0f && x < threshold + width * 0.5f)
            {
                float t = x - kneeStart;
                return x + (excessEfficiency - 1f) * t * t / (2f * width);
            }

            return threshold + (x - threshold) * excessEfficiency;
        }

        private float SoftCeiling(float x, float ceiling)
        {
            float start = ceiling * hitCapSoftStart;
            if (x <= start) return x;

            float range = ceiling - start;
            if (range <= 0f) return Mathf.Min(x, ceiling);

            return start + range * (1f - Mathf.Exp(-(x - start) / range));
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