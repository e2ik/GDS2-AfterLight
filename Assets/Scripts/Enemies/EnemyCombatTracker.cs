using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    public static class EnemyCombatTracker
    {
        private static readonly HashSet<Enemy> _targetingEnemies = new();

        public static int TargetingCount => _targetingEnemies.Count;

        public static void EnemyStartedTargeting(Enemy enemy)
        {
            bool wasEmpty = _targetingEnemies.Count == 0;
            if (!_targetingEnemies.Add(enemy)) return;
            
            if(wasEmpty)
                MusicManager.Instance?.SetTargetIntensity(0.8f);
            else
                MusicManager.AddIntensity(0.1f);
        }

        public static void EnemyStoppedTargeting(Enemy enemy)
        {
            if (!_targetingEnemies.Remove(enemy)) return;

            if (_targetingEnemies.Count == 0)
                MusicManager.Instance?.SetTargetIntensity(0.5f);
        }
    }
}
