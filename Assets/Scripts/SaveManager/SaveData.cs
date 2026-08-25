using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public PlayerStatsSaveData playerStats;
    public List<ItemInstanceSaveData> inventory; // example not sure if will use
    public ItemInstanceSaveData equippedWeapon;
    public List<EquippedGearSaveData> equippedGear = new List<EquippedGearSaveData>();
    public SecondaryGemInstance equippedSecondaryGem;
    public ProgressSaveData progress;
    public ChestSaveData chestData;
    public InventorySaveData inventoryData;
}

[System.Serializable]
public class EquippedGearSaveData
{
    public EGearSlot slot;
    public GearInstance gearData;
}

[System.Serializable]
public class PlayerStatsSaveData
{
    // placeholder incase we are increasing base stats via items
    public int attack;
    public int defense;
    public int humanity;
}

[System.Serializable]
public class ItemInstanceSaveData
{
}

[System.Serializable]
public class ProgressSaveData
{
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
    public List<SecondaryGemInstance> secondaryGems = new List<SecondaryGemInstance>();
    public List<GearInstance> gearInstances = new List<GearInstance>();
    public int equippedSecondaryGemIndex = -1;
}