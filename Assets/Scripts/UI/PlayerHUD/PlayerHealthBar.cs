using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image healthFillImage;

    private PlayerStats currentStats;

    public void BindStats(PlayerStats stats)
    {
        if (currentStats != null)
        {
            currentStats.OnHealthChanged -= UpdateHealthBar;
        }

        currentStats = stats;

        if (currentStats != null)
        {
            currentStats.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(currentStats.CurrentHealth, currentStats.MaxHealth);
        }
    }

    private void OnDestroy()
    {
        if (currentStats != null)
        {
            currentStats.OnHealthChanged -= UpdateHealthBar;
        }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthFillImage == null || maxHealth <= 0f) return;
        healthFillImage.fillAmount = currentHealth / maxHealth;
    }
}