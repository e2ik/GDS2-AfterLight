using UnityEngine;

namespace Enemies
{
    public class FollowCollider2D : MonoBehaviour
    {
        private Collider2D target;

        public void SetTarget(Collider2D col)
        {
            target = col;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.bounds.center;
        }

        private void OnDisable()
        {
            target = null;
        }
    }
}