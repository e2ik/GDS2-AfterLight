using System.Collections.Generic;

[System.Serializable]
public class StatRoll
{
    public string statName;
    public int value;
}

[System.Serializable]
public class ItemInstance
{
    public string instanceId;
    public string itemId;
    public int rolledDamage;
    public List<StatRoll> rolledStats = new();

    public ItemInstanceSaveData ToSaveData()
    {
        return new ItemInstanceSaveData
        {
            instanceId = instanceId,
            itemId = itemId,
            rolledDamage = rolledDamage,
            rolledStats = rolledStats
        };
    }

    public static ItemInstance FromSaveData(ItemInstanceSaveData data)
    {
        return new ItemInstance
        {
            instanceId = data.instanceId,
            itemId = data.itemId,
            rolledDamage = data.rolledDamage,
            rolledStats = data.rolledStats
        };
    }
}