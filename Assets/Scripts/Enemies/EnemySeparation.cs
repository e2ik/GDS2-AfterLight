using System.Collections.Generic;
using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    public static class EnemySeparation
    {
        private static readonly List<Collider2D> results = new List<Collider2D>();
        private static readonly HashSet<Rigidbody2D> counted = new HashSet<Rigidbody2D>();

        public static Vector2 GetPush(EnemyContext ctx, float radius, float strength, LayerMask mask)
        {
            if (radius <= 0f || strength <= 0f || mask.value == 0 || ctx.Body == null) return Vector2.zero;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(mask);
            filter.useTriggers = false;

            Vector2 self = ctx.Body.position;
            Physics2D.OverlapCircle(self, radius, filter, results);

            Vector2 push = Vector2.zero;
            counted.Clear();

            foreach (Collider2D col in results)
            {
                Rigidbody2D other = col.attachedRigidbody;
                if (other == null || other == ctx.Body || !counted.Add(other)) continue;
                if (other.GetComponent<Enemy>() == null) continue;

                Vector2 away = self - other.position;
                float distance = away.magnitude;

                if (distance < 0.001f)
                {
                    away = Random.insideUnitCircle.normalized;
                    distance = 0.001f;
                }

                push += away / distance * (1f - Mathf.Clamp01(distance / radius));
            }

            return push * strength;
        }
    }
}
