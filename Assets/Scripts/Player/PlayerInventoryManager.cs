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
            SaveManager.Instance?.SaveInventory(ToSaveData());
        }
    }

    public void AddItemToInventory(GearInstance item)
    {
        if (item == null || currentInventory == null) return;

        if (!currentInventory.GearInstances.Contains(item))
        {
            currentInventory.GearInstances.Add(item);
            SaveManager.Instance?.SaveInventory(ToSaveData());
        }
    }

    public InventorySaveData ToSaveData()
    {
        var data = new InventorySaveData();
        if (currentInventory != null)
        {
            data.secondaryGems.AddRange(currentInventory.SecondaryGems);
            data.gearInstances.AddRange(currentInventory.GearInstances);
        }
        return data;
    }

    public void LoadFromSaveData(InventorySaveData data)
    {
        if (currentInventory == null) return;

        currentInventory.SecondaryGems.Clear();
        currentInventory.GearInstances.Clear();

        if (data == null) return;

        if (data.secondaryGems != null)
        {
            currentInventory.SecondaryGems.AddRange(data.secondaryGems);
        }

        if (data.gearInstances != null)
        {
            currentInventory.GearInstances.AddRange(data.gearInstances);
        }
    }
}