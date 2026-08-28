using System;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class HurtBox : MonoBehaviour
    {
        [SerializeField] private EnemyHealth health;
        public bool Invulnerable;

        private void Start()
        {
            health = transform.parent.parent.GetComponent<EnemyHealth>();
        }
    }
}