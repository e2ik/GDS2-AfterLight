using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergyBar : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image energyFillImage;

    private PlayerCombatController currentStats;

    public void BindStats(PlayerCombatController stats)
    {
        if (currentStats != null)
        {
            currentStats.OnEnergyChanged -= UpdateEnergyBar;
        }

        currentStats = stats;

        if (currentStats != null)
        {
            currentStats.OnEnergyChanged += UpdateEnergyBar;
            UpdateEnergyBar(currentStats.SkillMeter, 1f);
        }
    }

    private void OnDestroy()
    {
        if (currentStats != null)
        {
            currentStats.OnEnergyChanged -= UpdateEnergyBar;
        }
    }

    private void UpdateEnergyBar(float currentEnergy, float maxEnergy)
    {
        if (energyFillImage == null || maxEnergy <= 0f) return;
        energyFillImage.fillAmount = currentEnergy / maxEnergy;
    }
}