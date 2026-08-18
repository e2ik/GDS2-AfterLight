using UnityEngine;
using System.Collections.Generic;

public class ItemGenTestRunner : MonoBehaviour
{
    public ItemSO testItem;
    public SaveManager saveManager;

    private List<ItemInstance> inventory = new();

    [ContextMenu("Generate Item")]
    public void GenerateItem()
    {
        var instance = ItemGenerator.Generate(testItem);
        inventory.Add(instance);
        Debug.Log($"Generated {instance.itemId}, damage: {instance.rolledDamage}");
    }

    [ContextMenu("Save Inventory")]
    public void SaveInventory()
    {
        var data = new SaveData
        {
            inventory = inventory.ConvertAll(i => i.ToSaveData())
        };
        saveManager.SaveGame(data);
    }

    [ContextMenu("Load Inventory")]
    public void LoadInventory()
    {
        var data = saveManager.LoadGame();
        if (data == null) return;

        inventory = data.inventory.ConvertAll(ItemInstance.FromSaveData);
        Debug.Log($"Loaded {inventory.Count} item(s):");
        foreach (var item in inventory)
            Debug.Log($"{item.itemId} — damage: {item.rolledDamage}");
    }

    [ContextMenu("Clear Inventory")]
    public void ClearInventory()
    {
        inventory.Clear();
        Debug.Log("Inventory cleared.");
    }
}