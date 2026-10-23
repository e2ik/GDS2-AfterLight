using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    public static class EnemyCombatTracker
    {
        private static readonly HashSet<Enemy> _targetingEnemies = new();
        private static readonly HashSet<Enemy> _activeEnemies = new();

        public static int TargetingCount => _targetingEnemies.Count;
        public static IEnumerable<Enemy> TargetingEnemies => _targetingEnemies;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _targetingEnemies.Clear();
            _activeEnemies.Clear();
        }

        public static void Register(Enemy enemy)
        {
            if (enemy != null) _activeEnemies.Add(enemy);
        }

        public static void Unregister(Enemy enemy)
        {
            _activeEnemies.Remove(enemy);
        }

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

        public static bool AnyUnengagedOnScreen(Camera cam)
        {
            if (cam == null || _activeEnemies.Count == 0) return false;

            Rect view = GetCameraRect(cam);

            foreach (Enemy enemy in _activeEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || _targetingEnemies.Contains(enemy)) continue;

                EnemyContext ctx = enemy.Context;
                if (ctx == null || (ctx.Health != null && ctx.Health.CurrentHealth <= 0)) continue;

                Bounds body = EnemyTerrainProbe.GetBodyBounds(ctx);
                Rect rect = new Rect(body.min.x, body.min.y, body.size.x, body.size.y);

                if (view.Overlaps(rect)) return true;
            }

            return false;
        }

        private static Rect GetCameraRect(Camera cam)
        {
            if (cam.orthographic)
            {
                float height = cam.orthographicSize * 2f;
                float width = height * cam.aspect;
                Vector3 center = cam.transform.position;
                return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            }

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}