using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInventorySO", menuName = "Inventory/PlayerInventorySO")]
public class PlayerInventorySO : ScriptableObject
{
    public List<InventoryItemBase> inventoryItems;
}
