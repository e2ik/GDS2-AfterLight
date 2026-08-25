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
        }
    }
}
