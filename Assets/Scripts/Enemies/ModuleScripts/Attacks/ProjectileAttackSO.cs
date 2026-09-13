using Enemies.ProjectileScripts;
using UnityEngine;
using EventReference = FMODUnity.EventReference;

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

        [SerializeField] private EventReference launchEvent;
        
        public override void Begin(EnemyContext ctx)
        {
            ctx.OverrideController[ctx.PlaceholderClip] = clip;
            ctx.Animator.Play(attackStateName, 0, 0f);
            ctx.Animator.Update(0f);
            
            AudioManager.PlaySFXAttached(launchEvent, ctx.Self.gameObject);
            
            int facing = ctx.FacingRight ? 1 : -1;
            float startAngle = launchAngleDegrees - shotSpacingDegrees * (shotCount - 1) / 2f;

            for (int i = 0; i < shotCount; i++)
            {
                float angle = (startAngle + i * shotSpacingDegrees) * Mathf.Deg2Rad;
                Vector2 velocity = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle)) * launchSpeed;

                var proj = ProjectilePool.Get(projectilePrefab, ctx.Self.position, Quaternion.identity);
                proj.Launch(ctx.Self.position, velocity, damage, ctx.Self);
            }
        }

        public override bool IsFinished(EnemyContext ctx)
        {
            var state = ctx.Animator.GetCurrentAnimatorStateInfo(0);
            return state.normalizedTime >= 1f || !state.IsName("Attack");
        }
    }
}