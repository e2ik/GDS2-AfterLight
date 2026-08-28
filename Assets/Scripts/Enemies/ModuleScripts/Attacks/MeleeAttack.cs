using UnityEngine;

namespace Enemies.ModuleScripts.Attacks
{
    [CreateAssetMenu(menuName = "Enemies/Attack/Melee")]
    public class MeleeAttack : EnemyAttackSO
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField] private int damage = 1;

        [SerializeField] private string placeholderClipName = "AttackPlaceholder";
        [SerializeField] private string attackStateName = "Attack";

        public override void Begin(EnemyContext ctx)
        {
            ctx.OverrideController[placeholderClipName] = clip;

            var events = ctx.Self.GetComponentInChildren<AttackEvents>();
            events.CurrentDamage = damage;
            events.CurrentParryDirection = CombatUtility.GetAttackDirection(ctx.Self.position, ctx.TargetPosition);
            
            ctx.Animator.Play(attackStateName, 0, 0f);
        }

        public override bool IsFinished(EnemyContext ctx)
        {
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);

            return state.normalizedTime >= 1f && !state.IsName(attackStateName);
        } 
        
    }
}
