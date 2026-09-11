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

    public WeaponInstance CreateInstance(ERarity rarity)
    {
        Vector2 dmgRange = DamageByRarity.GetRange(rarity);
        Vector2 rangeRange = RangeByRarity.GetRange(rarity);
        Vector2 critRange = CritByRarity.GetRange(rarity);

        WeaponInstance newInstance = new WeaponInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstTemplateID = ItemID,
            Rarity = rarity,
            InstRolledDamage = BaseWeaponDamage + Random.Range(dmgRange.x, dmgRange.y),
            InstRolledRange = Random.Range(rangeRange.x, rangeRange.y),
            InstRolledCrit = Random.Range(critRange.x, critRange.y)
        };

        Debug.Log($"Created Weapon Instance: {UIName} [{rarity}]");
        return newInstance;
    }
}