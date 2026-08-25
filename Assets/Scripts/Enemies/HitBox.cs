using System;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class HitBox : MonoBehaviour
    {
        [SerializeField] private Collider2D col;
        public int Damage { get; private set; }
        public bool IsActive => col.enabled;

        private void Start()
        {
            col = GetComponent<Collider2D>();
            col.enabled = false;
        }

        public void Activate(int damage)
        {
            Damage = damage;
            col.enabled = true;
        }

        public void Deactivate() => col.enabled = false;
    }
}
