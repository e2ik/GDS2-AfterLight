using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string masterSceneName = "WorldMaster";
    [SerializeField] private string defaultStartSceneName = "StartingArea";
    [SerializeField] private string defaultSpawnAnchorID = "DefaultSpawn";

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;

    [Header("References")]
    [SerializeField] private WorldMapStateSO worldMapState;
    [SerializeField] private SaveManager saveManager;

    private GameObject _playerInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void NewGame()
    {
        StartCoroutine(NewGameRoutine());
    }

    public void LoadGame()
    {
        StartCoroutine(LoadGameRoutine());
    }

    private IEnumerator NewGameRoutine()
    {
        yield return LoadMasterSceneSingle();

        SpawnPlayer();

        worldMapState.ResetState();

        string savePath = Path.Combine(Application.persistentDataPath, "save.json");
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("[GameManager] Save file deleted for new game.");
        }

        yield return LoadSceneAdditive(defaultStartSceneName);
        PlacePlayerAtAnchor(defaultSpawnAnchorID);
    }

    private IEnumerator LoadGameRoutine()
    {
        yield return LoadMasterSceneSingle();

        SpawnPlayer();

        worldMapState.ResetState();

        SaveData data = saveManager.LoadGame();

        if (data == null)
        {
            Debug.LogWarning("[GameManager] No save data found — falling back to new game.");
            yield return LoadSceneAdditive(defaultStartSceneName);
            PlacePlayerAtAnchor(defaultSpawnAnchorID);
            yield break;
        }

        worldMapState.LoadFromSaveIDs(data.progress.unlockedFastTravelIDs);

        string sceneToLoad = string.IsNullOrEmpty(data.progress.lastVisitedSceneName)
            ? defaultStartSceneName
            : data.progress.lastVisitedSceneName;

        string anchorToUse = string.IsNullOrEmpty(data.progress.lastSpawnAnchorID)
            ? defaultSpawnAnchorID
            : data.progress.lastSpawnAnchorID;

        yield return LoadSceneAdditive(sceneToLoad);
        PlacePlayerAtAnchor(anchorToUse);
    }

    private IEnumerator LoadMasterSceneSingle()
    {
        AsyncOperation loadMaster = SceneManager.LoadSceneAsync(masterSceneName, LoadSceneMode.Single);
        while (!loadMaster.isDone)
        {
            yield return null;
        }
    }

    private IEnumerator LoadSceneAdditive(string sceneName)
    {
        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!load.isDone)
            {
                yield return null;
            }
        }
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[GameManager] Player prefab not assigned.");
            return;
        }

        if (_playerInstance != null)
        {
            Destroy(_playerInstance);
        }

        _playerInstance = Instantiate(playerPrefab);
        _playerInstance.name = "Player";
    }

    private void PlacePlayerAtAnchor(string anchorID)
    {
        if (_playerInstance == null)
        {
            Debug.LogWarning("[GameManager] No player instance to place at spawn anchor.");
            return;
        }

        if (FastTravelManager.Instance == null)
        {
            Debug.LogError("[GameManager] FastTravelManager.Instance missing — cannot resolve spawn anchor.");
            return;
        }

        Transform anchor = FastTravelManager.Instance.FindAnchorTransform(anchorID);
        if (anchor != null)
        {
            _playerInstance.transform.position = anchor.position;
        }
        else
        {
            Debug.LogWarning($"[GameManager] Spawn anchor '{anchorID}' not found.");
        }
    }
}