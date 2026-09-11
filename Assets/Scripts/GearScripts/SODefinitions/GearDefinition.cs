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

    public GearInstance CreateInstance(ERarity rarity)
    {
        Vector2 atkRange = AttackRanges.GetRange(rarity);
        Vector2 defRange = DefenseRanges.GetRange(rarity);
        Vector2 critRange = CritRanges.GetRange(rarity);

        GearInstance newInstance = new GearInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstTemplateID = TemplateID,
            Rarity = rarity,
            InstBonusAttack = Random.Range(atkRange.x, atkRange.y),
            InstBonusDefense = Random.Range(defRange.x, defRange.y),
            InstBonusCrit = Random.Range(critRange.x, critRange.y),
        };

        Debug.Log($"Created Gear Instance: {UIName} [{rarity}]");
        return newInstance;
    }
}