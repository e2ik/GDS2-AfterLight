using System;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class HitBox : MonoBehaviour
    {
        [SerializeField] private Collider2D col;
        public AttackEvents SourceEvents { get; private set; }
        public int Damage { get; private set; }
        public ParryDirection ParryDirection { get; private set; }
        public bool IsActive => col.enabled;

        private void Start()
        {
            col = GetComponent<Collider2D>();
            SourceEvents = GetComponentInParent<AttackEvents>();
            col.enabled = false;
        }

        public void Activate(int damage, ParryDirection parryDirection)
        {
            Damage = damage;
            ParryDirection = parryDirection;
        }
    }
}
