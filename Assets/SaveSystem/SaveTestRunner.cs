using UnityEngine;
using System.Collections.Generic;

public class SaveTestRunner : MonoBehaviour
{
    public PlayerStatsTest playerStats;
    public SaveManager saveManager;
    public EquipmentManager equipmentManager;
    public List<ItemInstance> inventory;

    [ContextMenu("Save")]
    public void Save()
    {
        var data = new SaveData
        {
            playerStats = playerStats.ToSaveData(),
            inventory = inventory.ConvertAll(i => i.ToSaveData()),
            equippedWeapon = equipmentManager.equippedWeapon?.ToSaveData() // null-safe
        };
        saveManager.SaveGame(data);
    }

    [ContextMenu("Load")]
    public void Load()
    {
        var data = saveManager.LoadGame();
        if (data == null) return;

        playerStats.LoadFromSaveData(data.playerStats);
        inventory = data.inventory.ConvertAll(ItemInstance.FromSaveData);

        if (data.equippedWeapon != null)
        {
            var weapon = ItemInstance.FromSaveData(data.equippedWeapon);
            equipmentManager.Equip(weapon);
        }
    }
}