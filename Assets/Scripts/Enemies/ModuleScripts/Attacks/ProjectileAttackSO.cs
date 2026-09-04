using Enemies.ProjectileScripts;
using UnityEngine;

namespace Enemies.ModuleScripts.Attacks
{
    [CreateAssetMenu(menuName = "Enemies/Attack/Projectile")]
    public class ProjectileAttackSO : EnemyAttackSO
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private int damage = 1;
        [SerializeField] private float launchAngleDegrees = 55f;
        [SerializeField] private float launchSpeed = 8f;
        [SerializeField] private int shotCount = 1;
        [SerializeField] private float shotSpacingDegrees = 12f;
 
        [SerializeField] private AnimationClip clip;
        [SerializeField] private string placeholderClipName = "AttackPlaceholder";
        [SerializeField] private string attackStateName = "Attack";
        
        public override void Begin(EnemyContext ctx)
        {
            ctx.OverrideController[placeholderClipName] = clip;
            ctx.Animator.Play(attackStateName, 0, 0f);
            
            int facing = ctx.FacingRight ? 1 : -1;
            float startAngle = launchAngleDegrees - shotSpacingDegrees * (shotCount - 1) / 2f;

            for (int i = 0; i < shotCount; i++)
            {
                float angle = (startAngle + i * shotSpacingDegrees) * Mathf.Deg2Rad;
                Vector2 velocity = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle)) * launchSpeed;

                var proj = ProjectilePool.Get(projectilePrefab, ctx.Self.position, Quaternion.identity);
                proj.Launch(ctx.Self.position, velocity, damage);
            }
        }

        public override bool IsFinished(EnemyContext ctx)
        {
            var state = ctx.Animator.GetCurrentAnimatorStateInfo(0);
            return state.normalizedTime >= 1f || !state.IsName("Attack");
        }
    }
}
