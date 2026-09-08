using UnityEngine;

public abstract class InventoryItemBase : ScriptableObject
{
    public string ItemID;
    public string UIName;
    public Sprite UISprite;

    public int maxCommonValue;
    public int maxRareValue;
    public int maxEpicValue;
    public int maxLegendaryValue;
}