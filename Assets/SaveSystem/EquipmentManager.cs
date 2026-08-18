using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public PlayerStatsTest playerStats;
    public ItemInstance equippedWeapon;

    public void Equip(ItemInstance item)
    {
        equippedWeapon = item;
        Recalculate();
        Debug.Log($"Equipped {item.itemId}");
    }

    public void Unequip()
    {
        if (equippedWeapon == null) return;
        Debug.Log($"Unequipped {equippedWeapon.itemId}");
        equippedWeapon = null;
        Recalculate();
    }

    private void Recalculate()
    {
        var equipped = new List<ItemInstance>();
        if (equippedWeapon != null) equipped.Add(equippedWeapon);
        playerStats.RecalculateStats(equipped);
    }
}
