using System;
using UnityEngine;

namespace Enemies
{
    [Serializable]
    public class AttackInstance
    {
        public EnemyAttackSO Attack;
        [Range(0f, 100f)] public float Weight = 50f;
        [SerializeField] private float maxDuration = 3f; // failsafe is animation fails

        [SerializeField] private bool manualOnly = false;
        public bool ManualOnly => manualOnly;
        
        private float _cooldownTimer;
        private float _elapsed;

        public bool InRange { get; private set; }
        public bool OnCooldown => _cooldownTimer > 0f;
        public bool IsValid => InRange && !OnCooldown && _canUse;
        public bool InRangeIfUsable { get; private set; }

        private bool _canUse = true;

        public void Tick(EnemyContext ctx, float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;

            InRange = Attack != null && ctx.Target != null &&
                      EnemyRange.DistanceToTarget(ctx) <= Attack.Range;

            _canUse = Attack != null && Attack.CanUse(ctx);
            InRangeIfUsable = InRange && _canUse;
        }

        public void Begin(EnemyContext ctx)
        {
            Attack.Begin(ctx);
            _cooldownTimer = Attack.CooldownDuration;
            _elapsed = 0f;
        }

        public bool IsFinished(EnemyContext ctx)
        {
            _elapsed += Time.deltaTime;

            if (ctx.Target == null)
                return true;

            if (_elapsed >= maxDuration)
                return true;

            return Attack.IsFinished(ctx);
        }
    }
}