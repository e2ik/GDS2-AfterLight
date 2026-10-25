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

        private enum RepositionPhase { None, WindUp, Move, WindDown }

        private RepositionPhase _phase;
        private float _repositionElapsed;
        private float _phaseElapsed;
        private bool _phaseEntered;
        private float _targetCenterX;
        private int _direction;
        private float _lastCenterX;
        private float _stuckTimer;
        private Enemy _enemy;

        public bool IsRepositioning => _phase != RepositionPhase.None;

        public void Tick(EnemyContext ctx, float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;

            InRange = Attack != null && ctx.Target != null &&
                      EnemyRange.DistanceToTarget(ctx) <= Attack.Range &&
                      Attack.IsInVerticalBand(ctx);

            _canUse = Attack != null && Attack.CanUse(ctx);
            InRangeIfUsable = InRange && _canUse;
        }

        public void Begin(EnemyContext ctx)
        {
            _elapsed = 0f;

            if (Attack.NeedsBoundaryReposition(ctx, out _targetCenterX, out _direction))
            {
                _enemy = ctx.Self != null ? ctx.Self.GetComponent<Enemy>() : null;
                _repositionElapsed = 0f;
                _stuckTimer = 0f;
                _lastCenterX = EnemyTerrainProbe.GetBodyBounds(ctx).center.x;
                _enemy?.FaceDirection(_direction);
                EnterPhase(ctx, RepositionPhase.WindUp);
                return;
            }

            BeginAttack(ctx);
        }

        private void BeginAttack(EnemyContext ctx)
        {
            _phase = RepositionPhase.None;
            Attack.Begin(ctx);
            _cooldownTimer = Attack.CooldownDuration;
            _elapsed = 0f;
        }

        public bool IsFinished(EnemyContext ctx)
        {
            if (ctx.Target == null)
            {
                CancelReposition(ctx);
                return true;
            }

            if (_phase != RepositionPhase.None)
                return TickReposition(ctx, Time.deltaTime);

            _elapsed += Time.deltaTime;

            if (_elapsed >= maxDuration)
                return true;

            return Attack.IsFinished(ctx);
        }

        private bool TickReposition(EnemyContext ctx, float dt)
        {
            _repositionElapsed += dt;
            _phaseElapsed += dt;
            _enemy?.KeepAttackAlive();

            if (_repositionElapsed >= Attack.RepositionTimeout)
            {
                if (Attack.AttackIfBlocked)
                {
                    StopMoving(ctx);
                    FinishReposition(ctx);
                    return false;
                }

                CancelReposition(ctx);
                _cooldownTimer = Attack.CooldownDuration;
                return true;
            }

            switch (_phase)
            {
                case RepositionPhase.WindUp:
                    StopMoving(ctx);
                    if (PhaseFinished(ctx, Attack.RepositionWindupState))
                    {
                        _lastCenterX = EnemyTerrainProbe.GetBodyBounds(ctx).center.x;
                        _stuckTimer = 0f;
                        _phase = RepositionPhase.Move;
                        _phaseElapsed = 0f;
                    }
                    return false;

                case RepositionPhase.Move:
                {
                    float centerX = EnemyTerrainProbe.GetBodyBounds(ctx).center.x;
                    bool arrived = _direction * (_targetCenterX - centerX) <= 0.02f;

                    if (Mathf.Abs(centerX - _lastCenterX) > 0.01f) _stuckTimer = 0f;
                    else _stuckTimer += dt;
                    _lastCenterX = centerX;

                    if (arrived || _stuckTimer >= 0.4f)
                    {
                        StopMoving(ctx);
                        EnterPhase(ctx, RepositionPhase.WindDown);
                        return false;
                    }

                    ctx.Body.linearVelocity = new Vector2(_direction * Attack.RepositionSpeed, ctx.Body.linearVelocity.y);
                    return false;
                }

                case RepositionPhase.WindDown:
                    StopMoving(ctx);
                    if (!PhaseFinished(ctx, Attack.RepositionWinddownState)) return false;

                    if (!Attack.AttackIfBlocked && Attack.NeedsBoundaryReposition(ctx, out _, out _))
                    {
                        CancelReposition(ctx);
                        _cooldownTimer = Attack.CooldownDuration;
                        return true;
                    }

                    FinishReposition(ctx);
                    return false;
            }

            return false;
        }

        private void FinishReposition(EnemyContext ctx)
        {
            ResetTriggers(ctx);
            _enemy?.FaceCurrentTarget();
            _enemy?.KeepAttackAlive();
            BeginAttack(ctx);
        }

        private void CancelReposition(EnemyContext ctx)
        {
            if (_phase == RepositionPhase.None) return;

            StopMoving(ctx);
            ResetTriggers(ctx);
            _phase = RepositionPhase.None;
        }

        private void EnterPhase(EnemyContext ctx, RepositionPhase phase)
        {
            _phase = phase;
            _phaseElapsed = 0f;
            _phaseEntered = false;

            string trigger = phase == RepositionPhase.WindUp ? Attack.RepositionWindupTrigger
                : phase == RepositionPhase.WindDown ? Attack.RepositionWinddownTrigger
                : null;

            if (!HasTrigger(ctx, trigger))
            {
                _phaseEntered = true;
                _phaseElapsed = Attack.RepositionPhaseTimeout;
                return;
            }

            ctx.Animator.SetTrigger(trigger);
        }

        private bool PhaseFinished(EnemyContext ctx, string stateName)
        {
            if (_phaseElapsed >= Attack.RepositionPhaseTimeout) return true;

            Animator animator = ctx.Animator;
            if (animator == null || string.IsNullOrEmpty(stateName)) return true;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateName))
            {
                _phaseEntered = true;
                return info.normalizedTime >= 1f;
            }

            return _phaseEntered;
        }

        private void ResetTriggers(EnemyContext ctx)
        {
            if (HasTrigger(ctx, Attack.RepositionWindupTrigger)) ctx.Animator.ResetTrigger(Attack.RepositionWindupTrigger);
            if (HasTrigger(ctx, Attack.RepositionWinddownTrigger)) ctx.Animator.ResetTrigger(Attack.RepositionWinddownTrigger);
        }

        private static void StopMoving(EnemyContext ctx)
        {
            if (ctx.Body != null) ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
        }

        private static bool HasTrigger(EnemyContext ctx, string trigger)
        {
            Animator animator = ctx.Animator;
            if (animator == null || string.IsNullOrEmpty(trigger)) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger) return true;

            return false;
        }
    }
}