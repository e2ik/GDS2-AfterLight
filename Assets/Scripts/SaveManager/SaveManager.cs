using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    
    public bool HasSaveFile => File.Exists(SavePath);
    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    [SerializeField] private WorldMapStateSO worldMapState;
    private SaveData _currentSaveData;
    public SaveData GetSaveData() => _currentSaveData;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (HasSaveFile)
        {
            LoadGame();
        }
        else
        {
            CreateNewSaveData();
        }
    }

    public void CreateNewSaveData()
    {
        _currentSaveData = new SaveData
        {
            progress = new ProgressSaveData(),
            chestData = new ChestSaveData(),
            inventoryData = new InventorySaveData()
        };
        
        SaveGame(_currentSaveData); 
    }

    public void SaveGame(SaveData data)
    {
        _currentSaveData = data;
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Saved to {SavePath}");
    }

    public SaveData LoadGame()
    {
        if (!HasSaveFile)
        {
            Debug.LogWarning("No save file found. Initializing default data.");
            CreateNewSaveData();
            return _currentSaveData;
        }

        string json = File.ReadAllText(SavePath);
        _currentSaveData = JsonUtility.FromJson<SaveData>(json);

        EnsureDataIntegrity();

        return _currentSaveData;
    }

    private void EnsureDataIntegrity()
    {
        if (_currentSaveData == null) _currentSaveData = new SaveData();
        if (_currentSaveData.progress == null) _currentSaveData.progress = new ProgressSaveData();
        if (_currentSaveData.chestData == null) _currentSaveData.chestData = new ChestSaveData();
        if (_currentSaveData.chestData.openedChestIDs == null) 
            _currentSaveData.chestData.openedChestIDs = new System.Collections.Generic.List<string>();
        if (_currentSaveData.inventoryData == null) _currentSaveData.inventoryData = new InventorySaveData();
    }

    public void SaveProgressAtLocation(string sceneName, string anchorID)
    {
        _currentSaveData.progress.lastVisitedSceneName = sceneName;
        _currentSaveData.progress.lastSpawnAnchorID = anchorID;
        if (worldMapState != null)
            _currentSaveData.progress.unlockedFastTravelIDs = worldMapState.ToSaveIDs();

        SaveGame(_currentSaveData);
    }

    public bool IsChestOpened(string chestID)
    {
        if (_currentSaveData?.chestData?.openedChestIDs == null) return false;
        return _currentSaveData.chestData.openedChestIDs.Contains(chestID);
    }

    public void MarkChestOpened(string chestID)
    {
        if (_currentSaveData?.chestData?.openedChestIDs == null) return;

        if (!_currentSaveData.chestData.openedChestIDs.Contains(chestID))
        {
            _currentSaveData.chestData.openedChestIDs.Add(chestID);
        }
        SaveGame(_currentSaveData);
    }

    public InventorySaveData GetInventorySaveData()
    {
        return _currentSaveData?.inventoryData;
    }

    public void SaveInventory(InventorySaveData data)
    {
        _currentSaveData.inventoryData = data;
        SaveGame(_currentSaveData);
    }
}