using System;
using UnityEngine;

namespace Enemies
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 10;
        public int CurrentHealth { get; private set; }

        public event Action<int, int> OnDamaged;
        public event Action OnDeath;

        private void Awake() => CurrentHealth = maxHealth;

        public void ApplyDamage(int amount)
        {
            if (CurrentHealth <= 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            
            OnDamaged?.Invoke(amount, CurrentHealth);
            
            if(CurrentHealth == 0)
                OnDeath?.Invoke();
        }
    }
}
