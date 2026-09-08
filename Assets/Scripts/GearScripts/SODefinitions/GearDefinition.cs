using UnityEngine;

public enum EGearSlot
{
    Armor,
    Boots
    // more if we decide to add more gear types in the future
}

[CreateAssetMenu(fileName = "NewGearDefinition", menuName = "ScriptableObjects/GearDefinition")]
public class GearDefinition : InventoryItemBase
{
    [Header("Gear Settings")]
    public EGearSlot Slot;
    public string TemplateID;

    [Header("Base Stat Ranges (Min / Max)")]
    public Vector2 BonusAttackRange = new Vector2(5f, 15f);
    public Vector2 BonusDefenseRange = new Vector2(2f, 8f);
    public Vector2 BonusCritRange = new Vector2(0f, 1f);

    public GearInstance CreateInstance(ERarity rarity)
    {
        // Rarity multiplier to scale stats upward for higher rarities
        float rarityMultiplier = GetRarityMultiplier(rarity);

        GearInstance newInstance = new GearInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstTemplateID = TemplateID,
            Rarity = rarity,
            InstBonusAttack = Random.Range(BonusAttackRange.x, BonusAttackRange.y) * rarityMultiplier,
            InstBonusDefense = Random.Range(BonusDefenseRange.x, BonusDefenseRange.y) * rarityMultiplier,
            InstBonusCrit = Random.Range(BonusCritRange.x, BonusCritRange.y) * rarityMultiplier,
        };

        Debug.Log($"Created Gear Instance: {UIName} [{rarity}]");
        return newInstance;
    }

    private int GetRolledValue(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common:
                return Random.Range(1,maxCommonValue);
            case ERarity.Rare:
                return Random.Range(maxCommonValue + 1, maxEpicValue);
            case ERarity.Epic:
                return Random.Range(maxRareValue + 1, maxEpicValue);
            case ERarity.Legendary:
                return Random.Range(maxEpicValue + 1, maxLegendaryValue);
            default:
                return 0;
        }
    }

    private float GetRarityMultiplier(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common: return 1.0f;
            case ERarity.Rare: return 1.3f;
            case ERarity.Epic: return 1.6f;
            case ERarity.Legendary: return 2.0f;
            default: return 1.0f;
        }
    }
}