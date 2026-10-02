using System.Text;
using UnityEngine;

public static class ItemTooltipTextBuilder
{
    private const string PositiveDiffColor = "#4CD964";
    private const string NegativeDiffColor = "#FF5252";
    private const string StatLabelColor = "#F5C542";

    private static string Label(string label) => $"<color={StatLabelColor}>{label}:</color>";

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

    private static string TriggerLabel(SGemType type)
    {
        switch (type)
        {
            case SGemType.Attack: return "OnHit";
            case SGemType.Skill: return "OnSkill";
            case SGemType.Parry: return "OnParry";
            default: return null;
        }
    }

    public static string BuildLootLineText(string itemName, ERarity? rarity, Color? overrideColor = null)
    {
        if (overrideColor.HasValue)
        {
            string colored = $"<color=#{ColorUtility.ToHtmlStringRGB(overrideColor.Value)}>{itemName}</color>";
            return $"You looted [{colored}]";
        }

        if (!rarity.HasValue)
        {
            return $"You looted [{itemName}]";
        }

        string coloredLabel = ColorizeByRarity(itemName, rarity.Value);
        return $"You looted [{coloredLabel}]";
    }

    public static string BuildGearTooltip(GearInstance gear, string slotName, GearInstance equippedComparison = null)
    {
        StringBuilder sb = new StringBuilder();
        string gearRarity = ColorizeByRarity(gear.Rarity.ToString(), gear.Rarity);
        sb.AppendLine(string.IsNullOrEmpty(slotName) ? gearRarity : $"{gearRarity}, {slotName}");

        int attack = (int)gear.InstBonusAttack;
        int defense = (int)gear.InstBonusDefense;
        int humanity = (int)gear.InstBonusHumanity;
        float crit = gear.InstBonusCrit;

        float? eqAttack = equippedComparison != null ? (float?)equippedComparison.InstBonusAttack : null;
        float? eqDefense = equippedComparison != null ? (float?)equippedComparison.InstBonusDefense : null;
        float? eqHumanity = equippedComparison != null ? (float?)equippedComparison.InstBonusHumanity : null;
        float? eqCrit = equippedComparison != null ? (float?)(equippedComparison.InstBonusCrit * 100f) : null;

        if (attack > 0) sb.AppendLine($"{Label("Attack")} +{attack}{BuildDiffSuffix(attack, eqAttack)}");
        if (defense > 0) sb.AppendLine($"{Label("Defense")} +{defense}{BuildDiffSuffix(defense, eqDefense)}");
        if (humanity > 0) sb.AppendLine($"{Label("Humanity")} +{humanity}{BuildDiffSuffix(humanity, eqHumanity)}");
        if (crit > 0) sb.AppendLine($"{Label("Crit")} +{crit * 100f:F1}%{BuildDiffSuffix(crit * 100f, eqCrit, "F1", "%")}");

        return sb.ToString().TrimEnd();
    }

    public static string BuildSecondaryGemTooltip(SecondaryGemInstance gem, SecondaryGemInstance equippedComparison = null)
    {
        StringBuilder sb = new StringBuilder();
        string rarityText = ColorizeByRarity(gem.Rarity.ToString(), gem.Rarity);
        string trigger = TriggerLabel(gem.Type);
        sb.AppendLine(trigger != null ? $"{rarityText}, {trigger}" : rarityText);

        float? eqDamage = equippedComparison != null ? (float?)equippedComparison.InstRolledDamageValue : null;
        float? eqCrit = equippedComparison != null ? (float?)equippedComparison.InstRolledCritValue : null;

        if (gem.InstRolledDamageValue > 0) sb.AppendLine($"{Label("DMG")} +{gem.InstRolledDamageValue}{BuildDiffSuffix(gem.InstRolledDamageValue, eqDamage)}");
        if (gem.InstRolledCritValue > 0) sb.AppendLine($"{Label("CRIT")} +{gem.InstRolledCritValue}%{BuildDiffSuffix(gem.InstRolledCritValue, eqCrit, "0", "%")}");

        SecondaryGemInstance effectComparison = equippedComparison != null && equippedComparison.Type == gem.Type
            ? equippedComparison
            : null;

        if (gem.InstRolledDotPercent > 0)
        {
            float? eqDot = effectComparison != null && effectComparison.InstRolledDotPercent > 0
                ? (float?)effectComparison.InstRolledDotPercent
                : null;

            sb.AppendLine($"{Label("BLEED")} {gem.InstRolledDotPercent}%{BuildDiffSuffix(gem.InstRolledDotPercent, eqDot, "0", "%")} of hit");
        }

        if (gem.InstRolledChargeAmount > 0f)
        {
            float charge = gem.InstRolledChargeAmount * 100f;
            float? eqCharge = effectComparison != null && effectComparison.InstRolledChargeAmount > 0f
                ? (float?)(effectComparison.InstRolledChargeAmount * 100f)
                : null;

            sb.AppendLine($"{Label("ENERGY")} +{charge:F0}{BuildDiffSuffix(charge, eqCharge, "F0")}");
        }

        if (gem.InstRolledReflectPercent > 0f)
        {
            float reflect = gem.InstRolledReflectPercent * 100f;
            float? eqReflect = effectComparison != null && effectComparison.InstRolledReflectPercent > 0f
                ? (float?)(effectComparison.InstRolledReflectPercent * 100f)
                : null;

            sb.AppendLine($"{Label("REFLECT")} {reflect:F1}%{BuildDiffSuffix(reflect, eqReflect, "F1", "%")} of incoming damage");
        }

        if (gem.InstRolledMaxGambleMult > 0f)
        {
            float? eqGamble = effectComparison != null && effectComparison.InstRolledMaxGambleMult > 0f
                ? (float?)effectComparison.InstRolledMaxGambleMult
                : null;

            sb.AppendLine($"{Label("GAMBLE")} {gem.InstRolledMaxGambleMult:F1}x dmg{BuildDiffSuffix(gem.InstRolledMaxGambleMult, eqGamble, "F1", "x")}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildWeaponTooltip(WeaponInstance weapon, WeaponInstance equippedComparison = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{ColorizeByRarity(weapon.Rarity.ToString(), weapon.Rarity)}, Weapon");

        float? eqDamage = equippedComparison != null ? (float?)equippedComparison.InstRolledDamage : null;
        float? eqRange = equippedComparison != null ? (float?)equippedComparison.InstRolledRange : null;
        float? eqCrit = equippedComparison != null ? (float?)(equippedComparison.InstRolledCrit * 100f) : null;
        float? eqAttack = equippedComparison != null ? (float?)equippedComparison.InstRolledAttack : null;

        if (weapon.InstRolledDamage > 0) sb.AppendLine($"{Label("Damage")} {weapon.InstRolledDamage:F1}{BuildDiffSuffix(weapon.InstRolledDamage, eqDamage, "F1")}");
        if (weapon.InstRolledAttack > 0) sb.AppendLine($"{Label("Attack")} +{weapon.InstRolledAttack:F0}{BuildDiffSuffix(weapon.InstRolledAttack, eqAttack)}");
        if (weapon.InstRolledRange > 0) sb.AppendLine($"{Label("Range")} {weapon.InstRolledRange:F1}{BuildDiffSuffix(weapon.InstRolledRange, eqRange, "F1")}");
        if (weapon.InstRolledCrit > 0) sb.AppendLine($"{Label("Crit")} {weapon.InstRolledCrit * 100f:F1}%{BuildDiffSuffix(weapon.InstRolledCrit * 100f, eqCrit, "F1", "%")}");

        return sb.ToString().TrimEnd();
    }

    public static string BuildPrimaryGemTooltip(PrimaryGemBehaviourDefinition def)
    {
        return def.GemAttackDescription;
    }
}