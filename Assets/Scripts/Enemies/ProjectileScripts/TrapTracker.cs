using System.Collections.Generic;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    public static class TrapTracker
    {
        public const int MaxActiveTraps = 3;

        private static readonly List<TrapProjectile> activeTraps = new();

        public static int ActiveCount => activeTraps.Count;

        public static void Register(TrapProjectile trap)
        {
            if (!activeTraps.Contains(trap))
                activeTraps.Add(trap);
        }

        public static void Unregister(TrapProjectile trap)
        {
            activeTraps.Remove(trap);
        }
    }
}
