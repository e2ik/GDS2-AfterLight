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

    [Header("Area State")]
    [SerializeField] private AreaSide currentAreaSide = AreaSide.Exterior;
    public AreaSide CurrentAreaSide => currentAreaSide;

    public event System.Action<AreaSide> OnAreaSideChanged;

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

    [Header("Rarity Colors")]
    [SerializeField] private Color commonColor = new Color(0.69f, 0.69f, 0.69f);
    [SerializeField] private Color rareColor = new Color(0.56f, 0.76f, 1.0f);
    [SerializeField] private Color epicColor = new Color(0.78f, 0.61f, 1.0f);
    [SerializeField] private Color legendaryColor = new Color(1.0f, 0.71f, 0.44f);
    [SerializeField] private Color defaultRarityColor = Color.white;

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

        SpawnTempBackground(); // temp
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

    #region Area State

    public void SetAreaSide(AreaSide side)
    {
        if (currentAreaSide == side) return;
        currentAreaSide = side;
        OnAreaSideChanged?.Invoke(side);

        UpdateTempBackgroundTint(side); // temporary
    }

    public void ApplyAreaSide(AreaSide side)
    {
        SceneAreaState[] areaStates = FindObjectsByType<SceneAreaState>(FindObjectsSortMode.None);
        if (areaStates.Length == 0)
        {
            SetAreaSide(side);
            return;
        }

        foreach (var areaState in areaStates)
        {
            areaState.SetSide(side);
        }
    }

    #endregion

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

        if ((currentState == GameState.Title && newState == GameState.Pause) ||
            (currentState == GameState.Pause && newState == GameState.Title))
        {
            Debug.LogWarning($"[GameManager] Invalid state transition from {currentState} to {newState}");
            return;
        }

        currentState = newState;

        // temporary
        if (backgroundRoot != null)
        {
            bool shouldBeVisible = newState == GameState.Game || newState == GameState.Pause;
            backgroundRoot.gameObject.SetActive(shouldBeVisible);
            if (shouldBeVisible) hasInitializedParallax = false;
        }

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

        // whatever we assign in inspector gets cached here
        player.Equipment.RegisterInspectorAssignedStartingGear();

        SaveManager targetSaveManager = GetSaveManager();
        if (targetSaveManager != null)
        {
            targetSaveManager.CreateNewSaveData();
        }

        if (worldMapState != null)
        {
            worldMapState.ResetState();
        }

        yield return LoadSceneAdditive(defaultStartSceneName);
        yield return null;

        SetAreaSide(AreaSide.Interior);
        ApplyAreaSide(AreaSide.Interior);

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

        AreaSide savedSide = (data.progress != null) ? data.progress.lastAreaSide : AreaSide.Exterior;
        SetAreaSide(savedSide);
        ApplyAreaSide(savedSide);

        if (data.progress != null && !string.IsNullOrEmpty(data.progress.lastInteractedFastTravelID))
        {
            FastTravelNodeSO lastNode = FastTravelNodeResolver.GetByID(data.progress.lastInteractedFastTravelID);
            if (lastNode != null)
            {
                FastTravelManager.Instance?.SetLastInteractedNode(lastNode);
            }
            else
            {
                Debug.LogWarning($"[GameManager] Could not resolve saved fast travel node ID '{data.progress.lastInteractedFastTravelID}'.");
            }
        }

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
        SaveManager.Instance?.CommitToDisk();

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

    #region Respawn (Death Flow)

    public void ForceReloadAndRespawn(FastTravelNodeSO node, AreaSide side, System.Action onComplete)
    {
        if (node == null)
        {
            Debug.LogWarning("[GameManager] ForceReloadAndRespawn called with a null node.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(ForceReloadAndRespawnRoutine(node, side, onComplete));
    }

    private IEnumerator ForceReloadAndRespawnRoutine(FastTravelNodeSO node, AreaSide side, System.Action onComplete)
    {
        string targetScene = node.targetSceneName;

        Scene masterScene = SceneManager.GetSceneByName(masterSceneName);
        if (masterScene.isLoaded)
        {
            SceneManager.SetActiveScene(masterScene);
        }

        Scene existingTargetScene = SceneManager.GetSceneByName(targetScene);
        if (existingTargetScene.isLoaded)
        {
            AsyncOperation forcedUnload = SceneManager.UnloadSceneAsync(existingTargetScene);
            if (forcedUnload != null)
            {
                while (!forcedUnload.isDone)
                {
                    yield return null;
                }
            }
        }

        // Also clear out any other loaded area scenes so we return to a single, clean scene state
        List<Scene> scenesToUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != masterSceneName && scene.name != targetScene && scene.isLoaded)
            {
                scenesToUnload.Add(scene);
            }
        }

        foreach (Scene scene in scenesToUnload)
        {
            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(scene);
            if (asyncUnload != null)
            {
                while (!asyncUnload.isDone)
                {
                    yield return null;
                }
            }
        }

        yield return LoadSceneAdditive(targetScene);
        yield return null;

        SetAreaSide(side);
        ApplyAreaSide(side);

        PlacePlayerAtAnchor(node.spawnAnchorID);

        SaveManager.Instance?.SaveProgressAtLocation(targetScene, node.spawnAnchorID, side);

        onComplete?.Invoke();
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

        MusicManager musicManager = FindFirstObjectByType<MusicManager>();
        musicManager.SetState(MusicState.Explore);
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

        if (data.equippedWeapon != null && !string.IsNullOrEmpty(data.equippedWeapon.InstTemplateID))
        {
            p.Equipment.EquipWeapon(data.equippedWeapon);
        }
        else
        {
            p.Equipment.ClearWeapon();
        }
    }

    private void ClearPlayerEquipment()
    {
        Player p = FindFirstObjectByType<Player>();
        if (p == null || p.Equipment == null) return;

        p.Equipment.ClearAllGear();
        p.Equipment.ClearSecondaryGem();
        p.Equipment.ClearWeapon();
        // p.Equipment.ClearSpecialAttack(); <-- not yet implemented do not clear
    }

    #endregion

    public Color GetRarityColor(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common: return commonColor;
            case ERarity.Rare: return rareColor;
            case ERarity.Epic: return epicColor;
            case ERarity.Legendary: return legendaryColor;
            default: return defaultRarityColor;
        }
    }

    // temp code for background
    [Header("Temp Background (REMOVE LATER)")]
    [SerializeField] private GameObject tempBackgroundPrefab;
    [SerializeField] private Color backgroundNormalColor = Color.white;
    [SerializeField] private Color backgroundDarkenedColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField, Range(0f, 1f)] private float horizontalParallaxFactor = 0.1f;
    [SerializeField, Range(0f, 1f)] private float verticalParallaxFactor = 0.05f;
    [SerializeField] private int tileGridSize = 3;
    [SerializeField] private float tileOverlap = 0.02f;

    private SpriteRenderer[] tempBackgroundTiles;
    private Transform backgroundRoot;
    private Vector2 tileSize;
    private Vector3 lastPlayerPosition;
    private bool hasInitializedParallax;

    #region TEMP Background Sprite (remove later)

    private void SpawnTempBackground()
    {
        if (tempBackgroundPrefab == null) return;

        GameObject rootObj = new GameObject("TempBackgroundRoot");
        rootObj.transform.SetParent(transform);
        backgroundRoot = rootObj.transform;

        int total = tileGridSize * tileGridSize;
        tempBackgroundTiles = new SpriteRenderer[total];

        SpriteRenderer prefabSr = tempBackgroundPrefab.GetComponentInChildren<SpriteRenderer>();
        tileSize = prefabSr != null
            ? Vector2.Scale(prefabSr.sprite.bounds.size, tempBackgroundPrefab.transform.localScale)
            : Vector2.one;

        Vector2 spacing = new Vector2(tileSize.x - tileOverlap, tileSize.y - tileOverlap); // ADDED

        int half = tileGridSize / 2;
        int index = 0;

        for (int x = -half; x <= half; x++)
        {
            for (int y = -half; y <= half; y++)
            {
                GameObject tile = Instantiate(tempBackgroundPrefab, backgroundRoot);
                tile.transform.localPosition = new Vector3(x * spacing.x, y * spacing.y, 0f); // CHANGED — uses spacing instead of tileSize
                tempBackgroundTiles[index] = tile.GetComponentInChildren<SpriteRenderer>();
                index++;
            }
        }

        rootObj.SetActive(false);
    }

    private void Update()
    {
        UpdateTempBackgroundParallax();
        UpdateTempBackgroundTileWrap();
    }

    private void UpdateTempBackgroundParallax()
    {
        if (backgroundRoot == null || player == null) return;

        if (!hasInitializedParallax)
        {
            backgroundRoot.position = player.transform.position;
            lastPlayerPosition = player.transform.position;
            hasInitializedParallax = true;
            return;
        }

        Vector3 playerDelta = player.transform.position - lastPlayerPosition;
        lastPlayerPosition = player.transform.position;

        Vector3 backgroundDelta = new Vector3(
            playerDelta.x * horizontalParallaxFactor,
            -playerDelta.y * verticalParallaxFactor,
            0f
        );

        backgroundRoot.position += backgroundDelta;
    }

    private void UpdateTempBackgroundTileWrap()
    {
        if (tempBackgroundTiles == null || player == null) return;

        float wrapDistanceX = tileSize.x;
        float wrapDistanceY = tileSize.y;
        float gridExtentX = tileSize.x * tileGridSize;
        float gridExtentY = tileSize.y * tileGridSize;

        foreach (var tile in tempBackgroundTiles)
        {
            if (tile == null) continue;

            Vector3 tilePos = tile.transform.position;
            Vector3 diff = player.transform.position - tilePos;

            if (Mathf.Abs(diff.x) > wrapDistanceX * (tileGridSize / 2f))
                tilePos.x += Mathf.Sign(diff.x) * gridExtentX;

            if (Mathf.Abs(diff.y) > wrapDistanceY * (tileGridSize / 2f))
                tilePos.y += Mathf.Sign(diff.y) * gridExtentY;

            tile.transform.position = tilePos;
        }
    }

    private void UpdateTempBackgroundTint(AreaSide side)
    {
        if (tempBackgroundTiles == null) return;
        Color target = side == AreaSide.Interior ? backgroundDarkenedColor : backgroundNormalColor;
        foreach (var tile in tempBackgroundTiles)
        {
            if (tile != null) tile.color = target;
        }
    }

    #endregion
}