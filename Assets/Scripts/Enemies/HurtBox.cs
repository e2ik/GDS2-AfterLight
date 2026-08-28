using System;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class HurtBox : MonoBehaviour
    {
        public EnemyHealth Health { get; private set; }
        public bool Invulnerable;

        private void Start()
        {
            Health = transform.parent.parent.GetComponent<EnemyHealth>();
        }
    }
}