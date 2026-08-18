using UnityEngine;

public class EquipTestRunner : MonoBehaviour
{
    public ItemSO testItem;
    public EquipmentManager equipmentManager;

    private ItemInstance generatedItem;

    [ContextMenu("Generate Item")]
    public void GenerateItem()
    {
        generatedItem = ItemGenerator.Generate(testItem);
        Debug.Log($"Generated {generatedItem.itemId}, damage: {generatedItem.rolledDamage}");
        foreach (var stat in generatedItem.rolledStats)
            Debug.Log($"{stat.statName}: {stat.value}");
    }

    [ContextMenu("Equip Generated Item")]
    public void EquipItem()
    {
        if (generatedItem == null)
        {
            Debug.LogWarning("Generate an item first.");
            return;
        }
        equipmentManager.Equip(generatedItem);
    }

    [ContextMenu("Unequip")]
    public void UnequipItem()
    {
        equipmentManager.Unequip();
    }
}