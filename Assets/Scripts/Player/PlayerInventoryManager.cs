using UnityEngine;

public class PlayerInventoryManager : MonoBehaviour
{
    public PlayerInventorySO currentInventory;

    public void AddItemToInventory(InventoryItemBase item)
    {
        if (!currentInventory.PrimaryGems.Contains(item))
        {
            currentInventory.PrimaryGems.Add(item);
        }
    }

    public void AddItemToInventory(SecondaryGemInstance item)
    {
        if (!currentInventory.SecondaryGems.Contains(item))
        {
            currentInventory.SecondaryGems.Add(item);
            SaveManager.Instance.SaveInventory(ToSaveData());
        }
    }

    public InventorySaveData ToSaveData()
    {
        var data = new InventorySaveData();
        data.secondaryGems.AddRange(currentInventory.SecondaryGems);
        return data;
    }

    public void LoadFromSaveData(InventorySaveData data)
    {
        currentInventory.SecondaryGems.Clear();
        currentInventory.SecondaryGems.AddRange(data.secondaryGems);
    }

    // might need remove logic sometime in the future
}