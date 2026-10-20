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
        [SerializeField] private float shotSpacingDegrees = 12f;
        [SerializeField] protected int shotCount = 1;

        [Header("Spawn")] 
        [SerializeField] private bool useCustomSpawnPoint = false;
        
        [SerializeField] private AnimationClip clip;
        [SerializeField] private string placeholderClipName = "AttackPlaceholder";
        [SerializeField] private string attackStateName = "Attack";

        [SerializeField] private EventReference launchEvent;

        [Header("Anticipation")]
        [SerializeField] private bool playAnticipation = true;
        [SerializeField] private AttackForce attackForce = AttackForce.Light;
        [SerializeField, Min(0f)] private float anticipationDelay = 0.3f;
        
        public override void Begin(EnemyContext ctx)
        {
            ctx.OverrideController[ctx.PlaceholderClip] = clip;
            ctx.Animator.Play(attackStateName, 0, 0f);
            ctx.Animator.Update(0f);

            ctx.CurrentAttackForce = attackForce;
            if (ctx.AttackEvents != null) ctx.AttackEvents.CurrentAttackForce = attackForce;

            if (projectilePrefab != null && projectilePrefab.HandlesOwnAnticipation)
            {
                ctx.ProjectilePending = false;
                Fire(ctx);
                return;
            }

            if (playAnticipation && ctx.AttackEvents != null)
            {
                ctx.AttackEvents.PlayAnticipation();
                ctx.AttackEvents.HoldCharge(anticipationDelay);
            }

            if (playAnticipation && anticipationDelay > 0f)
            {
                ctx.ProjectilePending = true;
                ctx.ProjectileFireTime = Time.time + anticipationDelay;
                return;
            }

            ctx.ProjectilePending = false;
            Fire(ctx);
        }

        private void Fire(EnemyContext ctx)
        {
            if (ctx.AttackEvents != null) ctx.AttackEvents.StopCharge();

            AudioManager.PlaySFXAttached(launchEvent, ctx.Self.gameObject);

            Vector3 spawnPos = useCustomSpawnPoint && ctx.ProjectileSpawnPoint != null
                ? ctx.ProjectileSpawnPoint.position
                : ctx.Self.position;
            
            int facing = ctx.FacingRight ? 1 : -1;
            float startAngle = launchAngleDegrees - shotSpacingDegrees * (shotCount - 1) / 2f;

            for (int i = 0; i < shotCount; i++)
            {
                float angle = (startAngle + i * shotSpacingDegrees) * Mathf.Deg2Rad;
                Vector2 velocity = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle)) * launchSpeed;

                var proj = ProjectilePool.Get(projectilePrefab, spawnPos, Quaternion.identity);
                proj.Launch(spawnPos, velocity, damage, ctx.Self);
            }
        }

        public override bool IsFinished(EnemyContext ctx)
        {
            if (ctx.ProjectilePending)
            {
                if (Time.time < ctx.ProjectileFireTime) return false;

                ctx.ProjectilePending = false;
                Fire(ctx);
                return false;
            }

            var state = ctx.Animator.GetCurrentAnimatorStateInfo(0);
            return state.normalizedTime >= 1f || !state.IsName("Attack");
        }
    }
}