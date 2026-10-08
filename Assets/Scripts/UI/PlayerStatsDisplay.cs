using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerStatsDisplay : MonoBehaviour
{
    [Header("Stat Text Elements")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI humanityText;
    [SerializeField] private TextMeshProUGUI critText;

    [Header("Rarity Colors")]
    [SerializeField] private bool colorByAverageRarity = true;

    private const int GearAttackLine = 0;
    private const int GearDefenseLine = 1;
    private const int GearCritLine = 2;
    private const int WeaponCritLine = 2;
    private const int WeaponAttackLine = 3;
    private const int GemDamageLine = 0;
    private const int GemCritLine = 1;

    private PlayerStats playerStats;
    private PlayerEquipmentManager equipmentManager;

    private Color attackBaseColor;
    private Color defenseBaseColor;
    private Color humanityBaseColor;
    private Color critBaseColor;
    private bool baseColorsCached;

    private readonly List<ERarity> rarityBuffer = new List<ERarity>();

    private void Awake()
    {
        CacheBaseColors();
    }

    private void OnEnable()
    {
        if (playerStats == null)
        {
            Player player = Object.FindFirstObjectByType<Player>();
            if (player != null)
            {
                RegisterPlayer(player);
                return;
            }
        }

        if (playerStats != null)
        {
            playerStats.OnStatsRecalculated -= RefreshStatsUI;
            playerStats.OnStatsRecalculated += RefreshStatsUI;
            RefreshStatsUI();
        }
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

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

            if (playerStats != null && gameObject.activeInHierarchy)
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
            AttackContext attackContext = equipmentManager.GetModifiedAttackContext(isAttack: true);
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
            critText.text = $"{totalCrit * 100f:F1}";

        ApplyRarityColors();
    }

    private void CacheBaseColors()
    {
        if (baseColorsCached) return;

        if (attackText != null) attackBaseColor = attackText.color;
        if (defenseText != null) defenseBaseColor = defenseText.color;
        if (humanityText != null) humanityBaseColor = humanityText.color;
        if (critText != null) critBaseColor = critText.color;
        baseColorsCached = true;
    }

    private void ApplyRarityColors()
    {
        CacheBaseColors();

        bool useRarity = colorByAverageRarity && equipmentManager != null && GameManager.Instance != null;

        ApplyColor(attackText, attackBaseColor, useRarity ? CollectAttackRarities() : null);
        ApplyColor(defenseText, defenseBaseColor, useRarity ? CollectDefenseRarities() : null);
        ApplyColor(humanityText, humanityBaseColor, useRarity ? CollectHumanityRarities() : null);
        ApplyColor(critText, critBaseColor, useRarity ? CollectCritRarities() : null);
    }

    private static void ApplyColor(TextMeshProUGUI text, Color baseColor, List<ERarity> rarities)
    {
        if (text == null) return;

        if (rarities == null || rarities.Count == 0)
        {
            text.color = baseColor;
            return;
        }

        text.color = GameManager.Instance.GetRarityColor(AverageRarity(rarities));
    }

    private static ERarity AverageRarity(List<ERarity> rarities)
    {
        float sum = 0f;
        foreach (ERarity rarity in rarities) sum += (int)rarity;

        int rounded = Mathf.FloorToInt(sum / rarities.Count + 0.5f);
        return (ERarity)rounded;
    }

    private static ERarity LineRarity(List<ERarity> lineRarities, int lineIndex, ERarity fallback)
    {
        if (lineRarities == null || lineIndex < 0 || lineIndex >= lineRarities.Count) return fallback;
        return lineRarities[lineIndex];
    }

    private List<ERarity> CollectAttackRarities()
    {
        rarityBuffer.Clear();

        foreach (GearInstance gear in EquippedGear())
        {
            if (gear.InstBonusAttack > 0f)
                rarityBuffer.Add(LineRarity(gear.LineRarities, GearAttackLine, gear.Rarity));
        }

        WeaponInstance weapon = equipmentManager.EquippedWeapon;
        if (weapon != null && weapon.InstRolledAttack > 0f)
            rarityBuffer.Add(LineRarity(weapon.LineRarities, WeaponAttackLine, weapon.Rarity));

        SecondaryGemInstance gem = EquippedGem();
        if (gem != null && gem.InstRolledDamageValue > 0)
            rarityBuffer.Add(LineRarity(gem.LineRarities, GemDamageLine, gem.Rarity));

        return rarityBuffer;
    }

    private List<ERarity> CollectDefenseRarities()
    {
        rarityBuffer.Clear();

        foreach (GearInstance gear in EquippedGear())
        {
            if (gear.InstBonusDefense > 0f)
                rarityBuffer.Add(LineRarity(gear.LineRarities, GearDefenseLine, gear.Rarity));
        }

        return rarityBuffer;
    }

    private List<ERarity> CollectHumanityRarities()
    {
        rarityBuffer.Clear();

        foreach (GearInstance gear in EquippedGear())
        {
            if (gear.InstBonusHumanity > 0f)
                rarityBuffer.Add(gear.Rarity);
        }

        return rarityBuffer;
    }

    private List<ERarity> CollectCritRarities()
    {
        rarityBuffer.Clear();

        foreach (GearInstance gear in EquippedGear())
        {
            if (gear.InstBonusCrit > 0f)
                rarityBuffer.Add(LineRarity(gear.LineRarities, GearCritLine, gear.Rarity));
        }

        WeaponInstance weapon = equipmentManager.EquippedWeapon;
        if (weapon != null && weapon.InstRolledCrit > 0f)
            rarityBuffer.Add(LineRarity(weapon.LineRarities, WeaponCritLine, weapon.Rarity));

        SecondaryGemInstance gem = EquippedGem();
        if (gem != null && gem.InstRolledCritValue > 0)
            rarityBuffer.Add(LineRarity(gem.LineRarities, GemCritLine, gem.Rarity));

        return rarityBuffer;
    }

    private IEnumerable<GearInstance> EquippedGear()
    {
        if (equipmentManager.EquippedGear == null) yield break;

        foreach (KeyValuePair<EGearSlot, GearInstance> slot in equipmentManager.EquippedGear)
        {
            if (slot.Value != null) yield return slot.Value;
        }
    }

    private SecondaryGemInstance EquippedGem()
    {
        SecondaryGemInstance gem = equipmentManager.SecondaryGem;
        return gem != null && !string.IsNullOrEmpty(gem.InstTemplateID) ? gem : null;
    }
}