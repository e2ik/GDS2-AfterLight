using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "ScriptableObjects/WeaponDefinition")]
public class WeaponDefinition : InventoryItemBase
{
    public float BaseWeaponDamage;
    public float BaseWeaponRange;
    public float BaseWeaponCrit;
}