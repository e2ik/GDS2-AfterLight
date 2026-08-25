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

        public void Begin(EnemyContext ctx)
        {
            ctx.OverrideController[placeholderClipName] = clip;

            var events = ctx.Self.GetComponent<AttackEvents>();
            events.CurrentDamage = damage;
            
            ctx.Animator.Play(attackStateName, 0, 0f);
        }

        public bool IsFinished(EnemyContext ctx) => ctx.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            throw new System.NotImplementedException();
        }
    }
}
