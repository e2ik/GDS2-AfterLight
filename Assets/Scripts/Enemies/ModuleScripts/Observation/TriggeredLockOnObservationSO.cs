using UnityEngine;

namespace Enemies.ModuleScripts.Observation
{
    [CreateAssetMenu(menuName = "Enemies/Observation/Triggered Lock-On")]
    public class TriggeredLockOnObservationSO : EnemyObservationSO
    {
        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (ctx.Target == null)
            {
                ctx.TargetVisible = false;
                return;
            }

            ctx.TargetVisible = true;
            ctx.TargetPosition = ctx.Target.position;
            ctx.LastKnownTargetPosition = ctx.TargetPosition;
            ctx.TimeSinceTargetSeen = 0f;
        }

        public void Trigger(EnemyContext ctx, Transform target)
        {
            ctx.Target = target;
            ctx.TargetVisible = true;
            ctx.TargetPosition = target.position;
            ctx.LastKnownTargetPosition = target.position;
            ctx.TimeSinceTargetSeen = 0f;
        }

        public void Release(EnemyContext ctx)
        {
            ctx.Target = null;
            ctx.TargetVisible = false;
        }
    }
}

