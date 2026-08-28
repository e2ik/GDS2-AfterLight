using UnityEngine;

namespace Enemies
{
    public static class CombatUtility
    {
        public static ParryDirection GetAttackDirection(Vector2 attackerPos, Vector2 targetPos)
        {
            Vector2 diff = attackerPos - targetPos;

            if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
                return diff.x >= 0f ? ParryDirection.Right : ParryDirection.Left;

            return diff.y >= 0f ? ParryDirection.Up : ParryDirection.Down;
        }  
    }
}
