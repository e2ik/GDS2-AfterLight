using UnityEngine;
using TMPro;

public class PlayerStatsDisplay : MonoBehaviour
{
    [Header("Stat Text Elements")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI humanityText;
    [SerializeField] private TextMeshProUGUI critText;

    private PlayerStats playerStats;
    private PlayerEquipmentManager equipmentManager;

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public void RegisterPlayer(Player player)
    {
        UnbindEvents();

        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
            equipmentManager = player.GetComponent<PlayerEquipmentManager>();

            if (playerStats != null)
            {
                playerStats.OnStatsRecalculated += RefreshStatsUI;
            }

            RefreshStatsUI();
        }
    }

    private void UnbindEvents()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsRecalculated -= RefreshStatsUI;
        }
    }

    public void RefreshStatsUI()
    {
        if (playerStats == null)
        {
            Player player = Object.FindFirstObjectByType<Player>();
            if (player == null) return;
            RegisterPlayer(player);
            return;
        }

        float totalAttack = playerStats.TotalAttack;
        float totalCrit = 0f;

        if (equipmentManager != null)
        {
            AttackContext attackContext = equipmentManager.GetModifiedAttackContext();
            totalAttack = attackContext.BaseAttackDamage; 
            totalCrit = attackContext.BaseAttackCrit;
        }

        float totalDefense = playerStats.TotalDefense;
        float totalHumanity = playerStats.TotalHumanity;

        if (attackText != null) 
            attackText.text = $"{totalAttack:F0}";

        if (defenseText != null) 
            defenseText.text = $"{totalDefense:F0}";

        if (humanityText != null) 
            humanityText.text = $"{totalHumanity:F0}";

        if (critText != null) 
            critText.text = $"{totalCrit:F1}%";
    }
}