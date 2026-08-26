using UnityEngine;

public class PlayerInventoryManager : MonoBehaviour
{
    public PlayerInventorySO currentInventory;
    public System.Action OnInventoryChanged;

    private void Start()
    {
        Player player = GetComponent<Player>();

        InventoryDisplay invDisplay = Object.FindFirstObjectByType<InventoryDisplay>();
        if (invDisplay != null)
        {
            invDisplay.RegisterInventoryManager(this);
        }

        PlayerEquipmentManager equipManager = GetComponent<PlayerEquipmentManager>();
        EquipmentDisplay equipDisplay = Object.FindFirstObjectByType<EquipmentDisplay>();
        if (equipDisplay != null && equipManager != null)
        {
            equipDisplay.RegisterEquipmentManager(equipManager);
        }

        PlayerStatsDisplay statsDisplay = Object.FindFirstObjectByType<PlayerStatsDisplay>();
        if (statsDisplay != null && player != null)
        {
            statsDisplay.RegisterPlayer(player);
        }
    }

    public void AddItemToInventory(SecondaryGemInstance item)
    {
        if (item == null || currentInventory == null) return;

        if (currentInventory.SecondaryGems == null)
            currentInventory.SecondaryGems = new System.Collections.Generic.List<SecondaryGemInstance>();

        currentInventory.SecondaryGems.Add(item);
        SaveManager.Instance?.SaveInventory(ToSaveData());

        OnInventoryChanged?.Invoke();
    }

    public void AddItemToInventory(GearInstance item)
    {
        if (item == null || currentInventory == null) return;

        if (currentInventory.GearInstances == null)
            currentInventory.GearInstances = new System.Collections.Generic.List<GearInstance>();

        currentInventory.GearInstances.Add(item);
        SaveManager.Instance?.SaveInventory(ToSaveData());

        OnInventoryChanged?.Invoke();
    }

    public InventorySaveData ToSaveData()
    {
        var data = new InventorySaveData();
        if (currentInventory != null)
        {
            if (currentInventory.SecondaryGems != null)
                data.secondaryGems.AddRange(currentInventory.SecondaryGems);

            if (currentInventory.GearInstances != null)
                data.gearInstances.AddRange(currentInventory.GearInstances);
        }
        return data;
    }

    public void LoadFromSaveData(InventorySaveData data)
    {
        if (currentInventory == null) return;

        currentInventory.SecondaryGems?.Clear();
        currentInventory.GearInstances?.Clear();

        if (data == null) return;

        if (data.secondaryGems != null && currentInventory.SecondaryGems != null)
            currentInventory.SecondaryGems.AddRange(data.secondaryGems);

        if (data.gearInstances != null && currentInventory.GearInstances != null)
            currentInventory.GearInstances.AddRange(data.gearInstances);

        OnInventoryChanged?.Invoke();
    }
}