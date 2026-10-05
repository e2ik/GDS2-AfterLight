using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "ScriptableObjects/WeaponDefinition")]
public class WeaponDefinition : InventoryItemBase
{
    public float BaseWeaponDamage;

    [Header("Range by Rarity")]
    public RarityRange RangeByRarity = new RarityRange
    {
        Common = new Vector2(1f, 3f),
        Rare = new Vector2(3f, 5f),
        Epic = new Vector2(5f, 10f),
        Legendary = new Vector2(15f, 20f)
    };

    [Header("Crit by Rarity (0-1)")]
    public RarityRange CritByRarity = new RarityRange
    {
        Common = new Vector2(0.03f, 0.05f),
        Rare = new Vector2(0.05f, 0.10f),
        Epic = new Vector2(0.10f, 0.15f),
        Legendary = new Vector2(0.15f, 0.20f)
    };

    [Header("Damage by Rarity")]
    public RarityRange DamageByRarity = new RarityRange
    {
        Common = new Vector2(1f, 3f),
        Rare = new Vector2(3f, 5f),
        Epic = new Vector2(5f, 10f),
        Legendary = new Vector2(15f, 20f)
    };

    [Header("Attack by Rarity")]
    public RarityRange AttackByRarity = new RarityRange
    {
        Common = new Vector2(1f, 3f),
        Rare = new Vector2(3f, 6f),
        Epic = new Vector2(6f, 10f),
        Legendary = new Vector2(10f, 15f)
    };

    public WeaponInstance CreateInstance(ERarity rarity) => Roll(RarityLineRoller.Fixed(rarity));

    public WeaponInstance Roll(RarityLineRoller roller)
    {
        WeaponInstance newInstance = new WeaponInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstTemplateID = ItemID,
            InstRolledDamage = BaseWeaponDamage + roller.RollValue(DamageByRarity),
            InstRolledRange = roller.RollValue(RangeByRarity),
            InstRolledCrit = roller.RollValue(CritByRarity),
            InstRolledAttack = Mathf.Round(roller.RollValue(AttackByRarity))
        };

        newInstance.Rarity = roller.Final;
        newInstance.LineRarities = new System.Collections.Generic.List<ERarity>(roller.Lines);

        // Debug.Log($"Created Weapon Instance: {UIName} [{newInstance.Rarity}]");
        return newInstance;
    }
}