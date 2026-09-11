using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "ScriptableObjects/WeaponDefinition")]
public class WeaponDefinition : InventoryItemBase
{
    public float BaseWeaponDamage;
    public float BaseWeaponRange;
    [Range(0f, 1f)] public float BaseWeaponCrit;

    public WeaponInstance CreateInstance(ERarity rarity)
    {
        float rarityMultiplier = GetRarityMultiplier(rarity);

        WeaponInstance newInstance = new WeaponInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstTemplateID = ItemID,
            Rarity = rarity,
            InstRolledDamage = BaseWeaponDamage * rarityMultiplier,
            InstRolledRange = BaseWeaponRange * rarityMultiplier,
            InstRolledCrit = Mathf.Clamp01(BaseWeaponCrit * rarityMultiplier)
        };

        Debug.Log($"Created Weapon Instance: {UIName} [{rarity}]");
        return newInstance;
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