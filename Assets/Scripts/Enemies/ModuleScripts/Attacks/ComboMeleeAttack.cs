using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Enemies.ModuleScripts.Attacks
{
    [CreateAssetMenu(menuName = "Enemies/Attack/Melee Combo")]
    public class ComboMeleeAttack : EnemyAttackSO
    {
        [Serializable]
        public class ComboStep
        {
            public AnimationClip clip;
            public int damage = 1;
            public AttackForce attackForce = AttackForce.Light;
            public string attackStateName = "Attack";

            public bool requiresHitToContinue = true;
        }

        [SerializeField] private List<ComboStep> steps = new();

        public override void Begin(EnemyContext ctx)
        {
            ctx.ComboStepIndex = 0;

            if (ctx.AttackEvents == null)
                ctx.AttackEvents = ctx.Self.GetComponentInChildren<AttackEvents>();
            
            BeginStep(ctx);
        }

        public override bool IsFinished(EnemyContext ctx)
        {
            ComboStep step = steps[ctx.ComboStepIndex];
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);
            bool clipDone = state.normalizedTime >= 1f && !state.IsName(step.attackStateName);

            if (!clipDone)
                return false;

            bool hasNextStep = ctx.ComboStepIndex < steps.Count - 1;
            bool allowedToContinue = !step.requiresHitToContinue || ctx.AttackEvents.LastHitConfirmed;

            if (hasNextStep && allowedToContinue)
            {
                ctx.ComboStepIndex++;
                BeginStep(ctx);
                return false;
            }

            return true;
        }

        private void BeginStep(EnemyContext ctx)
        {
            ComboStep step = steps[ctx.ComboStepIndex];

            ctx.OverrideController[ctx.PlaceholderClip] = step.clip;
            ctx.CurrentAttackForce = step.attackForce;

            ctx.AttackEvents.CurrentDamage = step.damage;
            ctx.AttackEvents.CurrentParryDirection =
                CombatUtility.GetAttackDirection(ctx.Self.position, ctx.TargetPosition);

            ctx.AttackEvents.CurrentAttackForce = step.attackForce;
            ctx.AttackEvents.ResetHitConfirmation();
            
            ctx.Animator.Play(step.attackStateName, 0, 0f);
            ctx.Animator.Update(0f);
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if(steps == null || steps.Count == 0)
                Debug.LogWarning($"{name}: combo has no steps configured");
        }
#endif
    }
}
