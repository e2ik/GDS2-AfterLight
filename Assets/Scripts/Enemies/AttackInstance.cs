using System;
using UnityEngine;

namespace Enemies
{
    [Serializable]
    public class AttackInstance
    {
        public EnemyAttackSO Attack;
        [Range(0f, 100f)] public float Weight = 50f;
        [SerializeField] private float leashMultiplier = 1.2f; // boundary buffer
        [SerializeField] private float maxDuration = 3f; // failsafe is animation fails

        private float _cooldownTimer;
        private float _elapsed;

        public bool InRange { get; private set; }
        public bool OnCooldown => _cooldownTimer > 0f;
        public bool IsValid => InRange && !OnCooldown;

        public void Tick(EnemyContext ctx, float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;

            InRange = Attack != null && ctx.Target != null &&
                      Vector2.Distance(ctx.Self.position, ctx.TargetPosition) <= Attack.Range;
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

            float distance = Vector2.Distance(ctx.Self.position, ctx.TargetPosition);
            if (distance > Attack.Range * leashMultiplier)
                return true;

            if (_elapsed >= maxDuration)
                return true;

            return Attack.IsFinished(ctx);
        }
    }
}