using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    
    public bool HasSaveFile => File.Exists(SavePath);
    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
    private string TempSavePath => Path.Combine(Application.persistentDataPath, "save.tmp");

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
        
        CommitToDisk(); 
    }

    public void CommitToDisk()
    {
        if (_currentSaveData == null) return;

        try
        {
            string json = JsonUtility.ToJson(_currentSaveData, prettyPrint: true);
            
            File.WriteAllText(TempSavePath, json);
            File.Copy(TempSavePath, SavePath, overwrite: true);
            File.Delete(TempSavePath);

            Debug.Log($"Saved successfully to {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to write save file: {e.Message}");
        }
    }

    public SaveData LoadGame()
    {
        if (!HasSaveFile)
        {
            Debug.LogWarning("No save file found. Initializing default data.");
            CreateNewSaveData();
            return _currentSaveData;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            _currentSaveData = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to load save file: {e.Message}");
            _currentSaveData = new SaveData();
        }

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
        if (_currentSaveData.equippedGear == null) 
            _currentSaveData.equippedGear = new System.Collections.Generic.List<EquippedGearSaveData>();
    }

    public void SaveProgressAtLocation(string sceneName, string anchorID)
    {
        _currentSaveData.progress.lastVisitedSceneName = sceneName;
        _currentSaveData.progress.lastSpawnAnchorID = anchorID;

        if (worldMapState != null)
        {
            _currentSaveData.progress.unlockedFastTravelIDs = worldMapState.ToSaveIDs();
        }

        Player player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            if (player.Inventory != null)
                _currentSaveData.inventoryData = player.Inventory.ToSaveData();

            if (player.Equipment != null)
            {
                _currentSaveData.equippedGear = player.Equipment.GetEquippedGearSaveData();
                _currentSaveData.equippedSecondaryGem = player.Equipment.SecondaryGem;
            }
        }

        CommitToDisk();
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
    }

    public InventorySaveData GetInventorySaveData()
    {
        return _currentSaveData?.inventoryData;
    }

    public void SaveInventory(InventorySaveData data)
    {
        _currentSaveData.inventoryData = data;
    }

    private void OnApplicationQuit()
    {
        CommitToDisk();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            CommitToDisk();
        }
    }
}