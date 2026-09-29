using System;
using UnityEngine;

namespace Enemies
{
    public class BossBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(10f, 6f);
        [SerializeField] private Color gizmoColor = Color.yellowGreen;

        public Bounds WorldBounds => new Bounds(transform.position, size);

        public Vector2 ClampPosition(Vector2 point)
        {
            Bounds b = WorldBounds;
            return new Vector2(
                Mathf.Clamp(point.x, b.min.x, b.max.x),
                Mathf.Clamp(point.y, b.min.y, b.max.y)
                );
        }

        public bool Contains(Vector2 point) => WorldBounds.Contains(point);

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
