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
            public AnimationClip placeholderClip;
            public AnimationClip clip;
            public int damage = 1;
            public AttackForce attackForce = AttackForce.Light;
            public string attackStateName = "Attack";

            public bool requiresHitToContinue = true;
        }

        [SerializeField] private List<ComboStep> steps = new();
        [SerializeField] private string comboBoolName = "ComboContinue";

        [Header("Random Continue")]
        [SerializeField, Range(0f, 1f)] private float continueChance = 1f;
        [SerializeField, Range(0f, 1f)] private float healthThreshold = 0f;
        [SerializeField, Range(0f, 1f)] private float continueChanceBelowThreshold = 1f;

        public override void Begin(EnemyContext ctx)
        {
            ctx.ComboStepIndex = 0;
            ctx.ComboNextArmed = false;
            ctx.Animator.SetBool(comboBoolName, false);

            if (ctx.AttackEvents == null)
                ctx.AttackEvents = ctx.Self.GetComponentInChildren<AttackEvents>();

            ApplyStepData(ctx, steps[0]);
            RollContinue(ctx);
            MarkTelegraph(ctx);
            
            ctx.Animator.Play(steps[0].attackStateName, 0, 0f);
            ctx.Animator.Update(0f);
        }

        public override bool IsFinished(EnemyContext ctx)
        {
            ComboStep step = steps[ctx.ComboStepIndex];
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);
            bool hasNextStep = ctx.ComboStepIndex < steps.Count - 1;
            MarkTelegraph(ctx);

            if (hasNextStep && !ctx.ComboNextArmed && !ctx.ComboStopped &&
                (!step.requiresHitToContinue || ctx.AttackEvents.LastHitConfirmed))
            {
                ArmNextStep(ctx);
            }
            
            if (ctx.ComboNextArmed)
            {
                ComboStep nextStep = steps[ctx.ComboStepIndex + 1];
                if (state.IsName(nextStep.attackStateName))
                {
                    ctx.ComboStepIndex++;
                    ctx.ComboNextArmed = false;
                    ctx.Animator.SetBool(comboBoolName, false);
                    RollContinue(ctx);
                }
 
                return false;
            }
            
            return state.IsName(step.attackStateName) && state.normalizedTime >= 1f;
        }

        private void ArmNextStep(EnemyContext ctx)
        {
            ComboStep nextStep = steps[ctx.ComboStepIndex + 1];
            ApplyStepData(ctx, nextStep);
            
            ctx.Animator.SetBool(comboBoolName, true);
            ctx.ComboNextArmed = true;
        }

        private void MarkTelegraph(EnemyContext ctx)
        {
            if (ctx.ComboStepIndex == 0 && steps.Count > 1 && !ctx.ComboStopped)
                ctx.ComboTelegraphFrame = Time.frameCount;
        }

        private void RollContinue(EnemyContext ctx)
        {
            float chance = continueChance;

            if (ctx.Health != null && ctx.Health.MaxHealth > 0 &&
                (float)ctx.Health.CurrentHealth / ctx.Health.MaxHealth <= healthThreshold)
            {
                chance = continueChanceBelowThreshold;
            }

            ctx.ComboStopped = chance < 1f && UnityEngine.Random.value >= chance;
        }

        private void ApplyStepData(EnemyContext ctx, ComboStep step)
        {
            ctx.OverrideController[step.placeholderClip] = step.clip;
            ctx.CurrentAttackForce = step.attackForce;

            ctx.AttackEvents.CurrentDamage = step.damage;
            ctx.AttackEvents.CurrentParryDirection =
                CombatUtility.GetAttackDirection(ctx.Self.position, ctx.TargetPosition);
            ctx.AttackEvents.CurrentAttackForce = step.attackForce;
            ctx.AttackEvents.ResetHitConfirmation();
        }
        
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning($"{name}: combo has no steps configured");
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if(steps[i].placeholderClip == null)
                    Debug.LogWarning($"{name}: step {i} has no placeholderClip assigned");
            }
            
        }
#endif
    }
}