using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public PlayerStatsSaveData playerStats;
    public List<ItemInstanceSaveData> inventory;
    public ItemInstanceSaveData equippedWeapon;
    public ProgressSaveData progress;
}

[System.Serializable]
public class PlayerStatsSaveData
{
    public int attack;
    public int defense;
    public int humanity;
}

[System.Serializable]
public class ItemInstanceSaveData
{
    public string instanceId;
    public string itemId;
    public int rolledDamage;
    public List<StatRoll> rolledStats;
}

[System.Serializable]
public class ProgressSaveData
{
    public List<string> storyFlags;
}