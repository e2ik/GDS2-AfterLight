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
        public AttackForce AttackForce { get; private set; }
        public bool IgnoreParryDirection { get; private set; }
        public bool IsActive => col.enabled;
        public bool HasBeenParried { get; private set; }
        public bool HasHit { get; private set; }

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            SourceEvents = GetComponentInParent<AttackEvents>();
            col.enabled = false;
        }

        public void Enable(int damage, ParryDirection parryDirection, AttackForce attackForce, bool ignoreParryDirection = false)
        {
            Damage = damage;
            col.enabled = true;
            ParryDirection = parryDirection;
            AttackForce = attackForce;
            IgnoreParryDirection = ignoreParryDirection;
            HasBeenParried = false;
            HasHit = false;
        }

        public void UpdateParryDirection(ParryDirection direction) => ParryDirection = direction;

        public void SetParried() => HasBeenParried = true;

        public void MarkHit() => HasHit = true;

        public void Disable() => col.enabled = false;

        public void ConfirmHit() => SourceEvents?.ConfirmHit();

    }
}