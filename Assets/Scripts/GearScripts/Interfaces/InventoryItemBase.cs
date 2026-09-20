using UnityEngine;

public abstract class InventoryItemBase : ScriptableObject
{
    public string ItemID;
    public string UIName;
    public Sprite UISprite;

    [Tooltip("If off, the player can't delete this item from the inventory.")]
    public bool Deletable = true;

    public int maxCommonValue;
    public int maxRareValue;
    public int maxEpicValue;
    public int maxLegendaryValue;
}