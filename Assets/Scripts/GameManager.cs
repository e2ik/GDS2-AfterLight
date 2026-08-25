using System.Collections;
using System.Collections.Generic;
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
    private Player player;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private SaveManager GetSaveManager()
    {
        return saveManager != null ? saveManager : SaveManager.Instance;
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

        if (worldMapState != null)
        {
            worldMapState.ResetState();
        }

        ClearPlayerInventory();
        ClearPlayerEquipment();

        SaveManager targetSaveManager = GetSaveManager();
        if (targetSaveManager != null)
        {
            targetSaveManager.CreateNewSaveData();
        }

        yield return LoadSceneAdditive(defaultStartSceneName);
        PlacePlayerAtAnchor(defaultSpawnAnchorID);
    }

    private IEnumerator LoadGameRoutine()
    {
        yield return LoadMasterSceneSingle();

        SpawnPlayer();

        if (worldMapState != null)
        {
            worldMapState.ResetState();
        }

        SaveManager targetSaveManager = GetSaveManager();
        SaveData data = targetSaveManager != null ? targetSaveManager.LoadGame() : null;

        if (data == null)
        {
            Debug.LogWarning("[GameManager] No save data found — falling back to new game.");
            yield return LoadSceneAdditive(defaultStartSceneName);
            PlacePlayerAtAnchor(defaultSpawnAnchorID);
            yield break;
        }

        if (worldMapState != null && data.progress != null)
        {
            worldMapState.LoadFromSaveIDs(data.progress.unlockedFastTravelIDs);
        }

        LoadPlayerInventory(data.inventoryData);
        LoadPlayerEquipment(data);

        string sceneToLoad = (data.progress != null && !string.IsNullOrEmpty(data.progress.lastVisitedSceneName))
            ? data.progress.lastVisitedSceneName
            : defaultStartSceneName;

        string anchorToUse = (data.progress != null && !string.IsNullOrEmpty(data.progress.lastSpawnAnchorID))
            ? data.progress.lastSpawnAnchorID
            : defaultSpawnAnchorID;

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
        player = _playerInstance.GetComponent<Player>();

        AssignCameraTarget();
    }

    private void AssignCameraTarget()
    {
        CameraFollow2D cam = FindFirstObjectByType<CameraFollow2D>();
        if (cam != null)
        {
            cam.SetTarget(_playerInstance.transform);
        }
        else
        {
            Debug.LogWarning("[GameManager] No CameraFollow2D found in scene to assign player target.");
        }
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

            CameraFollow2D cam = FindFirstObjectByType<CameraFollow2D>();
            cam?.SnapToTarget();
        }
        else
        {
            Debug.LogWarning($"[GameManager] Spawn anchor '{anchorID}' not found.");
        }
    }

    private void LoadPlayerInventory(InventorySaveData inventoryData)
    {
        if (_playerInstance == null) return;

        if (player.Inventory == null)
        {
            Debug.LogError("[GameManager] PlayerInventoryManager not found on player instance.");
            return;
        }

        player.Inventory.LoadFromSaveData(inventoryData);
    }

    private void ClearPlayerInventory()
    {
        if (_playerInstance == null) return;
        player.Inventory.LoadFromSaveData(new InventorySaveData());
    }

    private void LoadPlayerEquipment(SaveData data)
    {
        Player player = FindFirstObjectByType<Player>();
        if (player == null || player.Equipment == null) return;

        player.Equipment.LoadEquippedGearSaveData(data.equippedGear);

        if (data.equippedSecondaryGem != null && !string.IsNullOrEmpty(data.equippedSecondaryGem.InstTemplateID))
        {
            player.Equipment.EquipSecondaryGem(data.equippedSecondaryGem);
        }
        else
        {
            player.Equipment.ClearSecondaryGem();
        }
    }

    private void ClearPlayerEquipment()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player == null || player.Equipment == null) return;

        player.Equipment.ClearAllGear();
        player.Equipment.ClearSecondaryGem();
        player.Equipment.ClearWeapon();
        player.Equipment.ClearSpecialAttack();
    }
}