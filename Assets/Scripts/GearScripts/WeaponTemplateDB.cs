using UnityEngine;

[CreateAssetMenu(fileName = "WeaponTemplateDB", menuName = "Inventory/WeaponTemplateDB")]
public class WeaponTemplateDB : ScriptableObject
{
    public System.Collections.Generic.List<WeaponDefinition> weaponTemplates;
}
