using UnityEngine;

namespace Enemies.ModuleScripts.Attacks
{
    [CreateAssetMenu(menuName = "Enemies/Attack/Melee")]
    public class MeleeAttack : EnemyAttackSO
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackRange = 2;
        [SerializeField] private float attackCooldown = 3;

        [SerializeField] private string placeholderClipName = "AttackPlaceholder";
        [SerializeField] private string attackStateName = "Attack";

        public override void Begin(EnemyContext ctx)
        {
            ctx.OverrideController[placeholderClipName] = clip;

            var events = ctx.Self.GetComponent<AttackEvents>();
            events.CurrentDamage = damage;
            
            ctx.Animator.Play(attackStateName, 0, 0f);
        }
        public override bool IsFinished(EnemyContext ctx) => ctx.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
        
    }
}
