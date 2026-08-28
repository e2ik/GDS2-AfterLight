using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Title,
        Game,
        Pause
    }

    [Header("State Debug")]
    [SerializeField] private GameState currentState = GameState.Title;
    public GameState CurrentState => currentState;

    [Header("Scene Names")]
    [SerializeField] private string masterSceneName = "WorldMaster";
    [SerializeField] private string defaultStartSceneName = "StartingArea";
    [SerializeField] private string defaultSpawnAnchorID = "DefaultSpawn";
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;

    [Header("References")]
    [SerializeField] private WorldMapStateSO worldMapState;
    [SerializeField] private SaveManager saveManager;

    private GameObject _playerInstance;
    private Player player;
    public Player Player { get => player; }

    private UIManager uiManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private SaveManager GetSaveManager()
    {
        return saveManager != null ? saveManager : SaveManager.Instance;
    }

    private UIManager GetUIManager()
    {
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }
        return uiManager;
    }

    #region State Machine Logic

    public void TogglePause()
    {
        if (currentState == GameState.Game)
        {
            SetState(GameState.Pause);
        }
        else if (currentState == GameState.Pause)
        {
            SetState(GameState.Game);
        }
    }

    private void SetState(GameState newState)
    {
        if (currentState == newState) return;

        // Disallow transitioning directly between Title and Pause
        if ((currentState == GameState.Title && newState == GameState.Pause) ||
            (currentState == GameState.Pause && newState == GameState.Title))
        {
            Debug.LogWarning($"[GameManager] Invalid state transition from {currentState} to {newState}");
            return;
        }

        currentState = newState;

        switch (currentState)
        {
            case GameState.Title:
                Time.timeScale = 1f;
                GetUIManager()?.SetPauseCanvasActive(false);
                break;

            case GameState.Game:
                Time.timeScale = 1f;
                GetUIManager()?.SetPauseCanvasActive(false);

                if (player != null && player.Controller != null)
                {
                    player.Controller.FreezeMovement(false);
                }
                break;

            case GameState.Pause:
                Time.timeScale = 0f;
                GetUIManager()?.SetPauseCanvasActive(true);

                if (player != null && player.Controller != null)
                {
                    player.Controller.FreezeMovement(true);
                }
                break;
        }
    }

    #endregion

    #region Flow Routines

    public void NewGame()
    {
        StartCoroutine(NewGameRoutine());
    }

    public void LoadGame()
    {
        StartCoroutine(LoadGameRoutine());
    }

    public void ReturnToTitle()
    {
        StartCoroutine(ReturnToTitleRoutine());
    }

    private IEnumerator NewGameRoutine()
    {
        yield return LoadMasterSceneSingle();

        SpawnPlayer();

        ClearPlayerInventory();
        ClearPlayerEquipment();

        SaveManager targetSaveManager = GetSaveManager();
        if (targetSaveManager != null)
        {
            targetSaveManager.CreateNewSaveData();
        }

        yield return LoadSceneAdditive(defaultStartSceneName);
        yield return null;

        if (worldMapState != null)
        {
            worldMapState.ResetState();
        }

        PlacePlayerAtAnchor(defaultSpawnAnchorID);
        SetState(GameState.Game);
    }

    private IEnumerator LoadGameRoutine()
    {
        yield return LoadMasterSceneSingle();

        SpawnPlayer();

        SaveManager targetSaveManager = GetSaveManager();
        SaveData data = targetSaveManager != null ? targetSaveManager.LoadGame() : null;

        if (data == null)
        {
            Debug.LogWarning("[GameManager] No save data found — falling back to new game.");
            yield return LoadSceneAdditive(defaultStartSceneName);
            yield return null;
            
            if (worldMapState != null) worldMapState.ResetState();
            PlacePlayerAtAnchor(defaultSpawnAnchorID);

            SetState(GameState.Game);
            yield break;
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
        yield return null;

        if (worldMapState != null && data.progress != null && data.progress.unlockedFastTravelIDs != null)
        {
            worldMapState.LoadFromSaveIDs(data.progress.unlockedFastTravelIDs);
        }
        else if (worldMapState != null)
        {
            worldMapState.ResetState();
        }

        PlacePlayerAtAnchor(anchorToUse);
        SetState(GameState.Game);
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        SetState(GameState.Title);

        if (_playerInstance != null)
        {
            Destroy(_playerInstance);
            _playerInstance = null;
            player = null;
        }

        AsyncOperation loadTitle = SceneManager.LoadSceneAsync(titleSceneName, LoadSceneMode.Single);
        while (!loadTitle.isDone)
        {
            yield return null;
        }
    }

    #endregion

    #region Scene & Player Management

    private IEnumerator LoadMasterSceneSingle()
    {
        AsyncOperation loadMaster = SceneManager.LoadSceneAsync(masterSceneName, LoadSceneMode.Single);
        while (!loadMaster.isDone)
        {
            yield return null;
        }
        uiManager = FindFirstObjectByType<UIManager>();
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

    #endregion

    #region Inventory & Equipment Data Handling

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
        Player p = FindFirstObjectByType<Player>();
        if (p == null || p.Equipment == null) return;

        p.Equipment.LoadEquippedGearSaveData(data.equippedGear);

        if (data.equippedSecondaryGem != null && !string.IsNullOrEmpty(data.equippedSecondaryGem.InstTemplateID))
        {
            p.Equipment.EquipSecondaryGem(data.equippedSecondaryGem);
        }
        else
        {
            p.Equipment.ClearSecondaryGem();
        }
    }

    private void ClearPlayerEquipment()
    {
        Player p = FindFirstObjectByType<Player>();
        if (p == null || p.Equipment == null) return;

        p.Equipment.ClearAllGear();
        p.Equipment.ClearSecondaryGem();
        p.Equipment.ClearWeapon();
        p.Equipment.ClearSpecialAttack();
    }

    #endregion
}