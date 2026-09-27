using UnityEngine;

public abstract class InventoryItemBase : ScriptableObject
{
    public string ItemID;
    public string UIName;
    public Sprite UISprite;

    [Tooltip("If off, the player can't delete this item from the inventory.")]
    public bool Deletable = true;

    [Header("Inventory Sorting")]
    [Tooltip("Controls where this item's group sits relative to other groups in the inventory (lower = earlier).")]
    public int SortOrder = 0;

    public int maxCommonValue;
    public int maxRareValue;
    public int maxEpicValue;
    public int maxLegendaryValue;
}