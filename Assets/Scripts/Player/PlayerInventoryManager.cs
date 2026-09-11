using System.Linq;
using System.Collections.Generic;
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

    private void AddToList<T>(ref List<T> list, T item, System.Action<T, int> setPickupOrder) where T : class
    {
        if (item == null || currentInventory == null) return;

        if (list == null)
            list = new List<T>();

        setPickupOrder(item, nextPickupOrder++);
        list.Add(item);
        SaveManager.Instance?.SaveInventory(ToSaveData());

        OnInventoryChanged?.Invoke();
    }

    public void AddItemToInventory(SecondaryGemInstance item) =>
        AddToList(ref currentInventory.SecondaryGems, item, (i, order) => i.PickupOrder = order);

    public void AddItemToInventory(GearInstance item) =>
        AddToList(ref currentInventory.GearInstances, item, (i, order) => i.PickupOrder = order);

    public void AddItemToInventory(PrimaryGemInstance item) =>
        AddToList(ref currentInventory.PrimaryGems, item, (i, order) => i.PickupOrder = order);

    public void AddItemToInventory(WeaponInstance item) =>
        AddToList(ref currentInventory.Weapons, item, (i, order) => i.PickupOrder = order);

    private static void CopyIfPresent<T>(List<T> source, List<T> destination)
    {
        if (source != null) destination.AddRange(source);
    }

    public InventorySaveData ToSaveData()
    {
        var data = new InventorySaveData();
        if (currentInventory != null)
        {
            CopyIfPresent(currentInventory.SecondaryGems, data.secondaryGems);
            CopyIfPresent(currentInventory.GearInstances, data.gearInstances);
            CopyIfPresent(currentInventory.PrimaryGems, data.primaryGems);
            CopyIfPresent(currentInventory.Weapons, data.weapons);
        }
        return data;
    }

    public void LoadFromSaveData(InventorySaveData data)
    {
        if (currentInventory == null) return;

        currentInventory.SecondaryGems?.Clear();
        currentInventory.GearInstances?.Clear();
        currentInventory.PrimaryGems?.Clear();
        currentInventory.Weapons?.Clear();

        if (data == null)
        {
            nextPickupOrder = 0;
            return;
        }

        CopyIfPresent(data.secondaryGems, currentInventory.SecondaryGems);
        CopyIfPresent(data.gearInstances, currentInventory.GearInstances);
        CopyIfPresent(data.primaryGems, currentInventory.PrimaryGems);
        CopyIfPresent(data.weapons, currentInventory.Weapons);

        int highestLoadedOrder = -1;
        highestLoadedOrder = Mathf.Max(highestLoadedOrder, HighestOrder(currentInventory.SecondaryGems, g => g.PickupOrder));
        highestLoadedOrder = Mathf.Max(highestLoadedOrder, HighestOrder(currentInventory.GearInstances, g => g.PickupOrder));
        highestLoadedOrder = Mathf.Max(highestLoadedOrder, HighestOrder(currentInventory.PrimaryGems, g => g.PickupOrder));
        highestLoadedOrder = Mathf.Max(highestLoadedOrder, HighestOrder(currentInventory.Weapons, w => w.PickupOrder));

        nextPickupOrder = highestLoadedOrder + 1;

        OnInventoryChanged?.Invoke();
    }

    private static int HighestOrder<T>(List<T> list, System.Func<T, int> orderSelector)
    {
        return list != null && list.Count > 0 ? list.Max(orderSelector) : -1;
    }
}