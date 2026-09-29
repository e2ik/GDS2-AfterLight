using UnityEngine;

public enum EKeyClearance
{
    Zone1,
    Zone2,
    Zone3,
    Zone4
}

[CreateAssetMenu(fileName = "NewKeyDefinition", menuName = "ScriptableObjects/KeyDefinition")]
public class KeyDefinition : InventoryItemBase
{
    [Header("Key Settings")]
    public EKeyClearance Clearance;

    [TextArea(2, 5)]
    public string Description;

    public KeyInstance CreateInstance()
    {
        return new KeyInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstItemID = ItemID
        };
    }
}