using System;
using UnityEngine;

namespace Enemies
{
    [Serializable]
    public class AttackInstance
    {
        public EnemyAttackSO Attack;
        [Range(0f, 100f)] public float Weight = 50f;

        private float _cooldownTimer;
        
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
        }

        public bool IsFinished(EnemyContext ctx) => Attack.IsFinished(ctx);
    }
}
