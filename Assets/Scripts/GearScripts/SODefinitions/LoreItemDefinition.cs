using UnityEngine;

[CreateAssetMenu(fileName = "NewLoreItemDefinition", menuName = "ScriptableObjects/LoreItemDefinition")]
public class LoreItemDefinition : InventoryItemBase
{
    [Header("Lore Item Settings")]
    public Sprite FullImage;

    [TextArea(2, 5)]
    public string Description;

    [TextArea(5, 15)]
    public string ItemText;

    public LoreItemInstance CreateInstance()
    {
        return new LoreItemInstance
        {
            InstanceGUID = System.Guid.NewGuid().ToString(),
            InstItemID = ItemID
        };
    }
}