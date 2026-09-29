using UnityEngine;

[CreateAssetMenu(fileName = "NewLoreItemDefinition", menuName = "ScriptableObjects/LoreItemDefinition")]
public class LoreItemDefinition : InventoryItemBase
{
    [Header("Lore Item Settings")]
    public Sprite FullImage;

    [TextArea(2, 5)]
    public string Description;

    [Header("Lore Set (Multi-Piece Collectibles)")]
    [Tooltip("Leave blank for a standalone one-piece fragment. Give multiple pieces the same Set ID to have them collected together as one Codex entry.")]
    public string LoreSetID;

    [Tooltip("This piece's position within its set (1-based). Only matters when TotalPieces > 1.")]
    public int PieceIndex = 1;

    [Tooltip("How many pieces make up the complete set. Leave at 1 for a standalone fragment.")]
    public int TotalPieces = 1;

    public string EffectiveSetID => string.IsNullOrEmpty(LoreSetID) ? ItemID : LoreSetID;

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