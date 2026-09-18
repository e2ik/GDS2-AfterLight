using System.Text;
using UnityEngine;

public static class ItemTooltipTextBuilder
{
    private const string PositiveDiffColor = "#4CD964";
    private const string NegativeDiffColor = "#FF5252";

    public static string ColorizeByRarity(string text, ERarity rarity)
    {
        Color color = GameManager.Instance != null
            ? GameManager.Instance.GetRarityColor(rarity)
            : Color.white;

        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }

    private static string BuildDiffSuffix(float newValue, float? equippedValue, string format = "0", string unit = "")
    {
        if (!equippedValue.HasValue) return string.Empty;

        float diff = newValue - equippedValue.Value;
        if (Mathf.Approximately(diff, 0f)) return string.Empty;

        string sign = diff > 0f ? "+" : "-";
        string diffText = $"{sign}{Mathf.Abs(diff).ToString(format)}{unit}";
        string color = diff > 0f ? PositiveDiffColor : NegativeDiffColor;

        return $" <color={color}>({diffText})</color>";
    }

    public static string BuildLootLineText(string itemName, ERarity? rarity)
    {
        if (!rarity.HasValue)
        {
            return $"You looted [{itemName}]";
        }

        string coloredLabel = ColorizeByRarity($"{rarity.Value} {itemName}", rarity.Value);
        return $"You looted [{coloredLabel}]";
    }

    public static string BuildGearTooltip(GearInstance gear, string slotName, GearInstance equippedComparison = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Slot: {slotName}");
        sb.AppendLine(ColorizeByRarity($"Rarity: {gear.Rarity}", gear.Rarity));

        int attack = (int)gear.InstBonusAttack;
        int defense = (int)gear.InstBonusDefense;
        int humanity = (int)gear.InstBonusHumanity;
        float crit = gear.InstBonusCrit;

        float? eqAttack = equippedComparison != null ? (float?)equippedComparison.InstBonusAttack : null;
        float? eqDefense = equippedComparison != null ? (float?)equippedComparison.InstBonusDefense : null;
        float? eqHumanity = equippedComparison != null ? (float?)equippedComparison.InstBonusHumanity : null;
        float? eqCrit = equippedComparison != null ? (float?)(equippedComparison.InstBonusCrit * 100f) : null;

        if (attack > 0) sb.AppendLine($"Attack: +{attack}{BuildDiffSuffix(attack, eqAttack)}");
        if (defense > 0) sb.AppendLine($"Defense: +{defense}{BuildDiffSuffix(defense, eqDefense)}");
        if (humanity > 0) sb.AppendLine($"Humanity: +{humanity}{BuildDiffSuffix(humanity, eqHumanity)}");
        if (crit > 0) sb.AppendLine($"Crit: +{crit * 100f:F1}%{BuildDiffSuffix(crit * 100f, eqCrit, "F1", "%")}");

        return sb.ToString().TrimEnd();
    }

    public static string BuildSecondaryGemTooltip(SecondaryGemInstance gem, SecondaryGemInstance equippedComparison = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Type: Secondary Gem");
        sb.AppendLine(ColorizeByRarity($"Rarity: {gem.Rarity}", gem.Rarity));

        float? eqDamage = equippedComparison != null ? (float?)equippedComparison.InstRolledDamageValue : null;
        float? eqCrit = equippedComparison != null ? (float?)equippedComparison.InstRolledCritValue : null;
        float? eqDot = equippedComparison != null ? (float?)equippedComparison.InstRolledDotPercent : null;

        if (gem.InstRolledDamageValue > 0) sb.AppendLine($"Bonus Damage: +{gem.InstRolledDamageValue}{BuildDiffSuffix(gem.InstRolledDamageValue, eqDamage)}");
        if (gem.InstRolledCritValue > 0) sb.AppendLine($"Bonus Crit: +{gem.InstRolledCritValue}%{BuildDiffSuffix(gem.InstRolledCritValue, eqCrit, "0", "%")}");
        if (gem.InstRolledDotPercent > 0) sb.AppendLine($"Bleed: {gem.InstRolledDotPercent}%{BuildDiffSuffix(gem.InstRolledDotPercent, eqDot, "0", "%")} of hit damage over time");

        return sb.ToString().TrimEnd();
    }

    public static string BuildWeaponTooltip(WeaponInstance weapon, WeaponInstance equippedComparison = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ColorizeByRarity($"Rarity: {weapon.Rarity}", weapon.Rarity));

        float? eqDamage = equippedComparison != null ? (float?)equippedComparison.InstRolledDamage : null;
        float? eqRange = equippedComparison != null ? (float?)equippedComparison.InstRolledRange : null;
        float? eqCrit = equippedComparison != null ? (float?)(equippedComparison.InstRolledCrit * 100f) : null;

        if (weapon.InstRolledDamage > 0) sb.AppendLine($"Damage: {weapon.InstRolledDamage:F1}{BuildDiffSuffix(weapon.InstRolledDamage, eqDamage, "F1")}");
        if (weapon.InstRolledRange > 0) sb.AppendLine($"Range: {weapon.InstRolledRange:F1}{BuildDiffSuffix(weapon.InstRolledRange, eqRange, "F1")}");
        if (weapon.InstRolledCrit > 0) sb.AppendLine($"Crit: {weapon.InstRolledCrit * 100f:F1}%{BuildDiffSuffix(weapon.InstRolledCrit * 100f, eqCrit, "F1", "%")}");

        return sb.ToString().TrimEnd();
    }

    public static string BuildPrimaryGemTooltip(PrimaryGemBehaviourDefinition def)
    {
        // Primary gems don't roll rarity or instance stats — just show the attack description.
        return def.GemAttackDescription;
    }
}