using UnityEngine;

public class PlayerInventoryManager : MonoBehaviour
{
    public PlayerInventorySO currentInventory; 

    public void AddItemToInventory(InventoryItemBase item)
    {
        if (!currentInventory.inventoryItems.Contains(item))
        {
            currentInventory.inventoryItems.Add(item); 
        }
    }
}
