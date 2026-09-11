using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private TMP_Text healthText;

        [Header("Skill Meter")]
        [SerializeField] private Slider skillMeterBar;

        private PlayerStats stats;
        private PlayerCombatController combat;

        public void Bind(PlayerStats playerStats, PlayerCombatController playerCombat)
        {
            Unbind();

            stats = playerStats;
            combat = playerCombat;

            if (stats != null)
            {
                stats.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(stats.CurrentHealth, stats.MaxHealth);
            }

            if (combat != null)
            {
                combat.OnEnergyChanged += HandleEnergyChanged;
                HandleEnergyChanged(combat.SkillMeter, 1f);
            }
        }

        public void Unbind()
        {
            if (stats != null) { stats.OnHealthChanged -= HandleHealthChanged; }
            if (combat != null) { combat.OnEnergyChanged -= HandleEnergyChanged; }

            stats = null;
            combat = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (healthBar != null)
            {
                healthBar.maxValue = max;
                healthBar.SetValueWithoutNotify(current);
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void HandleEnergyChanged(float current, float max)
        {
            if (skillMeterBar != null)
            {
                skillMeterBar.maxValue = max;
                skillMeterBar.SetValueWithoutNotify(current);
            }
        }
    }
}