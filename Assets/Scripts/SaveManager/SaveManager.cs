using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    [SerializeField] private WorldMapStateSO worldMapState;

    private SaveData _currentSaveData = new SaveData
    {
        // other savedatatypes etc
        progress = new ProgressSaveData()
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("No save file found.");
            return null;
        }
        string json = File.ReadAllText(SavePath);
        _currentSaveData = JsonUtility.FromJson<SaveData>(json);
        return _currentSaveData;
    }

    public void SaveProgressAtLocation(string sceneName, string anchorID)
    {
        _currentSaveData.progress.lastVisitedSceneName = sceneName;
        _currentSaveData.progress.lastSpawnAnchorID = anchorID;
        _currentSaveData.progress.unlockedFastTravelIDs = worldMapState.ToSaveIDs();

        SaveGame(_currentSaveData);
    }
}