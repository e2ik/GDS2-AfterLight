using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public PlayerStatsSaveData playerStats;
    public List<ItemInstanceSaveData> inventory; // example
    public ItemInstanceSaveData equippedWeapon;
    public ProgressSaveData progress;
    public ChestSaveData chestData;
    public InventorySaveData inventoryData;
}

[System.Serializable]
public class PlayerStatsSaveData
{
    // store base stats, incase we add perma increase items
    // should recalculate on game load when player spawns in
    public int attack;
    public int defense;
    public int humanity;
}

[System.Serializable]
public class ItemInstanceSaveData
{
    // according to arie's video
    // we can store rolled stats + ID
    // we use this to repopulate inventory if required unless the SO takes care of that for us
}

[System.Serializable]
public class ProgressSaveData
{
    // store progress-related data
    public List<string> storyFlags;
    public List<string> unlockedFastTravelIDs;
    public string lastVisitedSceneName;
    public string lastSpawnAnchorID;
}

[System.Serializable]
public class ChestSaveData
{
    public List<string> openedChestIDs = new List<string>();
}

[System.Serializable]
public class InventorySaveData
{
    // primary and weapon not yet
    public List<SecondaryGemInstance> secondaryGems = new List<SecondaryGemInstance>();
}