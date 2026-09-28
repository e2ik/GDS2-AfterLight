using UnityEngine;

namespace Enemies
{
    public static class EnemyRange
    {
        public static float DistanceToTarget(EnemyContext ctx)
        {
            Vector2 targetPosition = ctx.Target != null ? (Vector2)ctx.Target.position : ctx.TargetPosition;
            return Vector2.Distance(ctx.Self.position, targetPosition);
        }
    }
}