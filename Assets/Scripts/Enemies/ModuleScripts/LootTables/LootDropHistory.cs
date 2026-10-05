using System.Collections.Generic;

public static class LootDropHistory
{
    private static readonly Dictionary<LootTableDefinitionSO, HashSet<string>> droppedThisCycle = new Dictionary<LootTableDefinitionSO, HashSet<string>>();

    public static string LastDroppedID { get; private set; }
    public static string LastDroppedType { get; private set; }

    public static string GetTypeKey(InventoryItemBase item)
    {
        if (item == null) return null;
        if (item is GearDefinition gear) return $"Gear:{gear.Slot}";
        return item.GetType().Name;
    }

    public static bool IsRepeat(InventoryItemBase item, LootTableDefinitionSO.ERepeatAvoidance avoidance)
    {
        if (item == null) return false;

        switch (avoidance)
        {
            case LootTableDefinitionSO.ERepeatAvoidance.SameItem:
                return LastDroppedID != null && item.ItemID == LastDroppedID;
            case LootTableDefinitionSO.ERepeatAvoidance.SameType:
                return (LastDroppedID != null && item.ItemID == LastDroppedID)
                    || (LastDroppedType != null && GetTypeKey(item) == LastDroppedType);
            default:
                return false;
        }
    }

    public static bool WasDroppedThisCycle(LootTableDefinitionSO table, InventoryItemBase item)
    {
        if (table == null || item == null) return false;
        return droppedThisCycle.TryGetValue(table, out HashSet<string> ids) && ids.Contains(item.ItemID);
    }

    public static void ResetCycle(LootTableDefinitionSO table)
    {
        if (table != null && droppedThisCycle.TryGetValue(table, out HashSet<string> ids)) ids.Clear();
    }

    public static void Record(InventoryItemBase item, LootTableDefinitionSO table = null)
    {
        if (item == null) return;

        LastDroppedID = item.ItemID;
        LastDroppedType = GetTypeKey(item);

        if (table == null) return;

        if (!droppedThisCycle.TryGetValue(table, out HashSet<string> ids))
        {
            ids = new HashSet<string>();
            droppedThisCycle[table] = ids;
        }
        ids.Add(item.ItemID);
    }

    public static void Clear()
    {
        droppedThisCycle.Clear();
        LastDroppedID = null;
        LastDroppedType = null;
    }
}