using System.Linq;
using UnityEngine;

public class PlayerInventoryManager : MonoBehaviour
{
    public PlayerInventorySO currentInventory;
    public System.Action OnInventoryChanged;

    private int nextPickupOrder = 0;

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

    // secondaries
    public void AddItemToInventory(SecondaryGemInstance item)
    {
        if (item == null || currentInventory == null) return;

        if (currentInventory.SecondaryGems == null)
            currentInventory.SecondaryGems = new System.Collections.Generic.List<SecondaryGemInstance>();

        item.PickupOrder = nextPickupOrder++; // NEW
        currentInventory.SecondaryGems.Add(item);
        SaveManager.Instance?.SaveInventory(ToSaveData());

        OnInventoryChanged?.Invoke();
    }

    // gear overload
    public void AddItemToInventory(GearInstance item)
    {
        if (item == null || currentInventory == null) return;

        if (currentInventory.GearInstances == null)
            currentInventory.GearInstances = new System.Collections.Generic.List<GearInstance>();

        item.PickupOrder = nextPickupOrder++; // NEW
        currentInventory.GearInstances.Add(item);
        SaveManager.Instance?.SaveInventory(ToSaveData());

        OnInventoryChanged?.Invoke();
    }

    // primary overload
    public void AddItemToInventory(PrimaryGemInstance item)
    {
        if (item == null || currentInventory == null) return;

        if (currentInventory.PrimaryGems == null)
            currentInventory.PrimaryGems = new System.Collections.Generic.List<PrimaryGemInstance>();

        item.PickupOrder = nextPickupOrder++;
        currentInventory.PrimaryGems.Add(item);
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

            if (currentInventory.PrimaryGems != null)
                data.primaryGems.AddRange(currentInventory.PrimaryGems);
        }
        return data;
    }

    public void LoadFromSaveData(InventorySaveData data)
    {
        if (currentInventory == null) return;

        currentInventory.SecondaryGems?.Clear();
        currentInventory.GearInstances?.Clear();
        currentInventory.PrimaryGems?.Clear();

        if (data == null)
        {
            nextPickupOrder = 0;
            return;
        }

        if (data.secondaryGems != null && currentInventory.SecondaryGems != null)
            currentInventory.SecondaryGems.AddRange(data.secondaryGems);

        if (data.gearInstances != null && currentInventory.GearInstances != null)
            currentInventory.GearInstances.AddRange(data.gearInstances);

        if (data.primaryGems != null && currentInventory.PrimaryGems != null)
            currentInventory.PrimaryGems.AddRange(data.primaryGems);

        int highestLoadedOrder = -1;
        if (currentInventory.SecondaryGems != null)
            highestLoadedOrder = Mathf.Max(highestLoadedOrder, currentInventory.SecondaryGems.Count > 0 ? currentInventory.SecondaryGems.Max(g => g.PickupOrder) : -1);
        if (currentInventory.GearInstances != null)
            highestLoadedOrder = Mathf.Max(highestLoadedOrder, currentInventory.GearInstances.Count > 0 ? currentInventory.GearInstances.Max(g => g.PickupOrder) : -1);
        if (currentInventory.PrimaryGems != null)
            highestLoadedOrder = Mathf.Max(highestLoadedOrder, currentInventory.PrimaryGems.Count > 0 ? currentInventory.PrimaryGems.Max(g => g.PickupOrder) : -1);


        nextPickupOrder = highestLoadedOrder + 1;

        OnInventoryChanged?.Invoke();
    }
}