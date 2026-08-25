using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInventorySO", menuName = "Inventory/PlayerInventorySO")]
public class PlayerInventorySO : ScriptableObject
{
    public List<InventoryItemBase> PrimaryGems;
    public List<SecondaryGemInstance> SecondaryGems;
    public List<GearInstance> GearInstances;
}
