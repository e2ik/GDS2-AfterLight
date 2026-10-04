using UnityEngine;

namespace Enemies.ModuleScripts.Attacks
{
    [CreateAssetMenu(menuName = "Enemies/Attack/Trap Projectile")]
    public class TrapProjectileAttackSO : ProjectileAttackSO
    {
        public override bool CanUse(EnemyContext ctx) =>
            Enemies.ProjectileScripts.TrapTracker.ActiveCount + shotCount <=
            Enemies.ProjectileScripts.TrapTracker.MaxActiveTraps;
    }
}