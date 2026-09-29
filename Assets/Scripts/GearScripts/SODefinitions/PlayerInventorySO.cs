using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInventorySO", menuName = "Inventory/PlayerInventorySO")]
public class PlayerInventorySO : ScriptableObject
{
    public List<PrimaryGemInstance> PrimaryGems;
    public List<SecondaryGemInstance> SecondaryGems;
    public List<GearInstance> GearInstances;
    public List<WeaponInstance> Weapons;
    public List<KeyInstance> KeyInstances;
    public List<LoreItemInstance> LoreItemInstances;

    [Header("Capacity Limits")]
    [Tooltip("Combined maximum for Weapons + Gear (they share the same filter tab).")]
    public int MaxWeaponsAndGear = 20;

    [Tooltip("Maximum number of Primary Gems (Skills) the player can hold.")]
    public int MaxPrimaryGems = 10;

    [Tooltip("Maximum number of Secondary Gems the player can hold.")]
    public int MaxSecondaryGems = 10;

    [Tooltip("Combined maximum for Keys + Lore Items (they share the Special filter tab).")]
    public int MaxSpecial = 20;
}