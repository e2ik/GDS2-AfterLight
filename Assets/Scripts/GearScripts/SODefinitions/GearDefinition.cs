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

    [Header("Rolled Stats")]
    public bool RollAttack = true;
    public bool RollDefense = true;
    public bool RollCrit = true;

    [Header("Attack by Rarity")]
    public RarityRange AttackRanges = new RarityRange
    {
        Common = new Vector2(1f, 3f),
        Rare = new Vector2(3f, 5f),
        Epic = new Vector2(5f, 10f),
        Legendary = new Vector2(15f, 20f)
    };

    [Header("Defense by Rarity")]
    public RarityRange DefenseRanges = new RarityRange
    {
        Common = new Vector2(1f, 3f),
        Rare = new Vector2(3f, 5f),
        Epic = new Vector2(5f, 10f),
        Legendary = new Vector2(15f, 20f)
    };

    [Header("Crit by Rarity (0-1)")]
    public RarityRange CritRanges = new RarityRange
    {
        Common = new Vector2(0.03f, 0.05f),
        Rare = new Vector2(0.05f, 0.10f),
        Epic = new Vector2(0.10f, 0.15f),
        Legendary = new Vector2(0.15f, 0.20f)
    };

    public GearInstance CreateInstance(ERarity rarity) => Roll(RarityLineRoller.Fixed(rarity));

    public GearInstance Roll(RarityLineRoller roller)
    {
        ERarity attackRarity = ERarity.Common;
        ERarity defenseRarity = ERarity.Common;
        ERarity critRarity = ERarity.Common;

        GearInstance newInstance = new GearInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstTemplateID = TemplateID,
            InstBonusAttack = RollAttack ? RollLine(roller, AttackRanges, out attackRarity) : 0f,
            InstBonusDefense = RollDefense ? RollLine(roller, DefenseRanges, out defenseRarity) : 0f,
            InstBonusCrit = RollCrit ? RollLine(roller, CritRanges, out critRarity) : 0f,
        };

        newInstance.Rarity = roller.Final;
        newInstance.LineRarities = new System.Collections.Generic.List<ERarity> { attackRarity, defenseRarity, critRarity };

        // Debug.Log($"Created Gear Instance: {UIName} [{newInstance.Rarity}]");
        return newInstance;
    }

    private static float RollLine(RarityLineRoller roller, RarityRange range, out ERarity lineRarity)
    {
        lineRarity = roller.Next();
        Vector2 minMax = range.GetRange(lineRarity);
        return Random.Range(minMax.x, minMax.y);
    }
}