using UnityEngine;

namespace Enemies.ModuleScripts.Attacks
{
    [CreateAssetMenu(menuName = "Enemies/Attack/Trap Projectile")]
    public class TrapProjectileAttackSO : ProjectileAttackSO
    {
        [SerializeField, Range(0f, 1f)] private float maxHealthFractionToUse = 0.5f;
        
        public override bool CanUse(EnemyContext ctx) =>
            HealthAllows(ctx) &&
            Enemies.ProjectileScripts.TrapTracker.ActiveCount + shotCount <=
            Enemies.ProjectileScripts.TrapTracker.MaxActiveTraps;

        private bool HealthAllows(EnemyContext ctx) => ctx.Health == null ||
                                                       ctx.Health.CurrentHealth <=
                                                       ctx.Health.MaxHealth * maxHealthFractionToUse;
    }
}