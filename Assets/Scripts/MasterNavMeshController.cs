using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MasterNavMeshController : MonoBehaviour
{
    public static MasterNavMeshController Instance { get; private set; }

    [SerializeField] private NavMeshPlus.Components.NavMeshSurface navMeshSurface;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float navMeshCheckRadius = 0.5f;
    [SerializeField] private bool asyncBake = true;

    public enum BakeCacheMode { Never, WebGLOnly, Always }
    [SerializeField] private BakeCacheMode cacheBakes = BakeCacheMode.WebGLOnly;
    [SerializeField] private bool debugBakeCache = false;
    [SerializeField] private Vector2 prebakeIsolationOffset = new Vector2(0f, 20000f);

    public bool IsBaking => pendingBake != null && !pendingBake.isDone;
    public bool CanPrebake => UsesBakeCache && navMeshSurface != null;

    private struct CachedBake
    {
        public NavMeshData Data;
        public Vector3 RootPosition;
        public Vector3 SurfacePosition;
        public Quaternion SurfaceRotation;
    }

    private readonly Dictionary<(int, int), CachedBake> bakeCache = new Dictionary<(int, int), CachedBake>();
    private (int, int) pendingCacheKey;
    private bool pendingCacheValid;
    private Vector3 pendingRootPosition;
    private Vector3 pendingSurfacePosition;
    private Quaternion pendingSurfaceRotation;

    private bool UsesBakeCache => cacheBakes == BakeCacheMode.Always
        || (cacheBakes == BakeCacheMode.WebGLOnly && Application.platform == RuntimePlatform.WebGLPlayer);

    private Scene currentScene;
    private bool hasCurrentScene;
    private bool dirty;
    private Transform playerTransform;
    private bool subscribedToAreaSide;
    private bool subscribedToStreamer;
    private AsyncOperation pendingBake;
    private readonly HashSet<int> preparedScenes = new HashSet<int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshPlus.Components.NavMeshSurface>();
        }
    }

    void Start()
    {
        TrySubscribe();

        // Auto-initialize to the active gameplay scene on startup if not already set
        if (!hasCurrentScene)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene != gameObject.scene && activeScene.isLoaded)
            {
                SetActiveScene(activeScene);
            }
        }
    }

    void OnEnable()
    {
        TrySubscribe();
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        preparedScenes.Remove(scene.handle);

        List<(int, int)> stale = new List<(int, int)>();
        foreach (KeyValuePair<(int, int), CachedBake> pair in bakeCache)
            if (pair.Key.Item1 == scene.handle) stale.Add(pair.Key);

        foreach ((int, int) key in stale)
        {
            if (bakeCache[key].Data != null && (navMeshSurface == null || navMeshSurface.navMeshData != bakeCache[key].Data))
                Destroy(bakeCache[key].Data);
            bakeCache.Remove(key);
        }
    }

    private static Vector3 GetRootPosition(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        return roots.Length > 0 ? roots[0].transform.position : Vector3.zero;
    }

    private (int, int) CacheKey(Scene scene)
    {
        int side = GameManager.Instance != null ? (int)GameManager.Instance.CurrentAreaSide : 0;
        return (scene.handle, side);
    }

    private bool TryUseCachedBake(Scene scene)
    {
        if (!UsesBakeCache || navMeshSurface == null) return false;
        if (!bakeCache.TryGetValue(CacheKey(scene), out CachedBake cached) || cached.Data == null) return false;

        Vector3 shift = GetRootPosition(scene) - cached.RootPosition;
        if (shift.sqrMagnitude > 0.0001f)
        {
            cached.RootPosition += shift;
            cached.SurfacePosition += shift;
            bakeCache[CacheKey(scene)] = cached;

            if (debugBakeCache) Debug.Log($"[MasterNavMeshController] Scene {scene.name} moved by {shift}, shifting its cached navmesh.");
        }

        navMeshSurface.RemoveData();
        navMeshSurface.transform.SetPositionAndRotation(cached.SurfacePosition, cached.SurfaceRotation);
        cached.Data.position = cached.SurfacePosition;
        cached.Data.rotation = cached.SurfaceRotation;
        navMeshSurface.navMeshData = cached.Data;
        navMeshSurface.AddData();

        if (debugBakeCache) Debug.Log($"[MasterNavMeshController] Reused cached navmesh for {scene.name}.");
        return true;
    }

    private void StoreBake(NavMeshData data, (int, int) key, Vector3 rootPosition, Vector3 surfacePosition, Quaternion surfaceRotation)
    {
        if (data == null) return;
        bakeCache[key] = new CachedBake
        {
            Data = data,
            RootPosition = rootPosition,
            SurfacePosition = surfacePosition,
            SurfaceRotation = surfaceRotation
        };
    }

    void OnDisable()
    {
        if (subscribedToAreaSide && GameManager.Instance != null)
        {
            GameManager.Instance.OnAreaSideChanged -= HandleAreaSideChanged;
        }

        if (subscribedToStreamer && WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.OnSceneStreamed -= HandleSceneStreamed;
        }

        subscribedToAreaSide = false;
        subscribedToStreamer = false;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void TrySubscribe()
    {
        if (!subscribedToAreaSide && GameManager.Instance != null)
        {
            GameManager.Instance.OnAreaSideChanged += HandleAreaSideChanged;
            subscribedToAreaSide = true;
        }

        if (!subscribedToStreamer && WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.OnSceneStreamed += HandleSceneStreamed;
            subscribedToStreamer = true;
        }
    }

    private static bool IsUsable(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded;
    }

    private void HandleSceneStreamed(Scene streamedScene)
    {
        SetActiveScene(streamedScene);
    }

    void Update()
    {
        if (!subscribedToAreaSide || !subscribedToStreamer) TrySubscribe();

        if (hasCurrentScene && !IsUsable(currentScene))
        {
            hasCurrentScene = false;
        }

        if (playerTransform == null)
        {
            Player player = FindFirstObjectByType<Player>();
            if (player != null) playerTransform = player.transform;
            return;
        }

        // Check if player is still standing on a valid baked NavMesh
        bool isOnNavMesh = NavMesh.SamplePosition(playerTransform.position, out NavMeshHit hit, navMeshCheckRadius, NavMesh.AllAreas);

        if (!isOnNavMesh || !hasCurrentScene)
        {
            Scene detectedScene = DetectSceneByPlayerPosition();
            if (IsUsable(detectedScene) && (!hasCurrentScene || detectedScene != currentScene))
            {
                SetActiveScene(detectedScene);
            }
        }
    }

    private Scene DetectSceneByPlayerPosition()
    {
        Scene persistentScene = gameObject.scene;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            
            // Skip the master/persistent scene where this controller lives
            if (scene == persistentScene) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Collider2D col = root.GetComponentInChildren<Collider2D>();
                if (col != null)
                {
                    if (col.bounds.Contains(playerTransform.position) || 
                        Vector2.Distance(playerTransform.position, col.transform.position) < 30f)
                    {
                        return scene;
                    }
                }
            }
        }
        return hasCurrentScene ? currentScene : default;
    }

    public void SetActiveScene(Scene scene)
    {
        if (!IsUsable(scene)) return;
        if (hasCurrentScene && scene == currentScene) return;

        currentScene = scene;
        hasCurrentScene = true;
        dirty = true;
    }

    void LateUpdate()
    {
        if (pendingBake != null)
        {
            if (!pendingBake.isDone) return;
            FinishAsyncBake();
        }

        if (!dirty || !hasCurrentScene) return;

        if (!IsUsable(currentScene))
        {
            hasCurrentScene = false;
            return;
        }

        ProcessLayerAndBake(currentScene);
        dirty = false;
    }

    private void HandleAreaSideChanged(AreaSide areaSide)
    {
        dirty = true;
    }

    private void ProcessLayerAndBake(Scene scene)
    {
        if (!IsUsable(scene))
        {
            Debug.LogWarning("[MasterNavMeshController] Tried to bake an invalid scene.");
            return;
        }

        if (playerTransform != null && navMeshSurface != null)
        {
            navMeshSurface.transform.position = playerTransform.position;
        }

        if (preparedScenes.Add(scene.handle)) PrepareModifiers(scene);

        if (navMeshSurface == null)
        {
            Debug.LogWarning("[MasterNavMeshController] NavMeshSurface reference is missing.");
            return;
        }

        if (TryUseCachedBake(scene)) return;

        bool caching = UsesBakeCache;
        (int, int) key = CacheKey(scene);
        Vector3 rootPosition = caching ? GetRootPosition(scene) : Vector3.zero;

        if (caching && navMeshSurface.navMeshData != null && IsCachedData(navMeshSurface.navMeshData))
        {
            NavMeshData fresh = new NavMeshData(navMeshSurface.agentTypeID);
            navMeshSurface.RemoveData();
            navMeshSurface.navMeshData = fresh;
        }

        if (asyncBake && navMeshSurface.navMeshData != null)
        {
            navMeshSurface.navMeshData.position = navMeshSurface.transform.position;
            navMeshSurface.navMeshData.rotation = navMeshSurface.transform.rotation;
            SuppressBrokenTilemaps();
            pendingBake = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            RestoreSuppressedTilemaps();

            if (pendingBake != null)
            {
                pendingCacheValid = caching;
                pendingCacheKey = key;
                pendingRootPosition = rootPosition;
                pendingSurfacePosition = navMeshSurface.transform.position;
                pendingSurfaceRotation = navMeshSurface.transform.rotation;
                return;
            }

            BuildSafely();
        }
        else
        {
            BuildSafely();
        }

        if (caching) StoreBake(navMeshSurface.navMeshData, key, rootPosition, navMeshSurface.transform.position, navMeshSurface.transform.rotation);
    }

    public void PrebakeScene(Scene scene)
    {
        if (!CanPrebake || !IsUsable(scene) || IsBaking) return;

        if (navMeshSurface.collectObjects != NavMeshPlus.Components.CollectObjects.Volume)
        {
            if (debugBakeCache) Debug.LogWarning("[MasterNavMeshController] Prebake needs the NavMeshSurface set to Collect Objects: Volume, skipping.");
            return;
        }

        if (!TryGetSceneBounds(scene, out Bounds sceneBounds)) return;

        if (GameManager.Instance != null) GameManager.Instance.ApplyAreaSide(GameManager.Instance.CurrentAreaSide);
        if (preparedScenes.Add(scene.handle)) PrepareModifiers(scene);

        Vector3 offset = prebakeIsolationOffset;
        GameObject[] roots = scene.GetRootGameObjects();
        NavMeshData previousData = navMeshSurface.navMeshData;
        Transform surfaceTransform = navMeshSurface.transform;
        Vector3 previousPosition = surfaceTransform.position;
        Quaternion previousRotation = surfaceTransform.rotation;
        bool moved = false;

        try
        {
            foreach (GameObject root in roots) root.transform.position += offset;
            moved = true;
            Physics2D.SyncTransforms();

            Vector3 bakeCenter = sceneBounds.center + offset;
            surfaceTransform.position = new Vector3(bakeCenter.x, bakeCenter.y, previousPosition.z);

            navMeshSurface.RemoveData();
            navMeshSurface.navMeshData = null;
            BuildSafely();

            NavMeshData baked = navMeshSurface.navMeshData;
            if (baked != null)
            {
                StoreBake(baked, CacheKey(scene), GetRootPosition(scene) - offset, surfaceTransform.position - offset, surfaceTransform.rotation);
                if (debugBakeCache) Debug.Log($"[MasterNavMeshController] Prebaked navmesh for {scene.name}.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
        }
        finally
        {
            navMeshSurface.RemoveData();
            surfaceTransform.SetPositionAndRotation(previousPosition, previousRotation);
            navMeshSurface.navMeshData = previousData;
            if (previousData != null) navMeshSurface.AddData();

            if (moved)
            {
                foreach (GameObject root in roots)
                    if (root != null) root.transform.position -= offset;
                Physics2D.SyncTransforms();
            }
        }
    }

    private static bool TryGetSceneBounds(Scene scene, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Collider2D col in root.GetComponentsInChildren<Collider2D>())
            {
                if (col == null || !col.enabled || col.isTrigger) continue;

                if (!found)
                {
                    bounds = col.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }
        }

        return found;
    }

    private readonly List<NavMeshPlus.Components.NavMeshModifier> suppressedModifiers = new List<NavMeshPlus.Components.NavMeshModifier>();
    private readonly List<GameObject> rootBuffer = new List<GameObject>();

    private void BuildSafely()
    {
        SuppressBrokenTilemaps();
        try
        {
            navMeshSurface.BuildNavMesh();
        }
        finally
        {
            RestoreSuppressedTilemaps();
        }
    }

    private void SuppressBrokenTilemaps()
    {
        suppressedModifiers.Clear();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            scene.GetRootGameObjects(rootBuffer);
            foreach (GameObject root in rootBuffer)
            {
                foreach (NavMeshPlus.Components.NavMeshModifier modifier in root.GetComponentsInChildren<NavMeshPlus.Components.NavMeshModifier>())
                {
                    if (modifier == null || modifier.ignoreFromBuild) continue;

                    UnityEngine.Tilemaps.Tilemap tilemap = modifier.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                    if (tilemap == null || tilemap.layoutGrid != null) continue;

                    modifier.ignoreFromBuild = true;
                    suppressedModifiers.Add(modifier);

                    if (debugBakeCache)
                        Debug.LogWarning($"[MasterNavMeshController] Skipping tilemap '{tilemap.name}' in scene '{scene.name}' for this bake: it has no Grid.", tilemap);
                }
            }
        }
    }

    private void RestoreSuppressedTilemaps()
    {
        foreach (NavMeshPlus.Components.NavMeshModifier modifier in suppressedModifiers)
            if (modifier != null) modifier.ignoreFromBuild = false;

        suppressedModifiers.Clear();
    }

    private bool IsCachedData(NavMeshData data)
    {
        foreach (CachedBake cached in bakeCache.Values)
            if (cached.Data == data) return true;
        return false;
    }

    private void FinishAsyncBake()
    {
        pendingBake = null;
        if (navMeshSurface == null) return;

        navMeshSurface.RemoveData();
        navMeshSurface.AddData();

        if (pendingCacheValid) StoreBake(navMeshSurface.navMeshData, pendingCacheKey, pendingRootPosition, pendingSurfacePosition, pendingSurfaceRotation);
        pendingCacheValid = false;
    }

    private void PrepareModifiers(Scene scene)
    {
        List<GameObject> sceneObjects = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                sceneObjects.Add(child.gameObject);
            }
        }

        foreach (GameObject obj in sceneObjects)
        {
            bool airOnlyPlatform = obj.GetComponent<AirOnlyCollisionPlatform>() != null;
            bool dropPlatform = obj.GetComponent<Collider2D>() != null
                                && (obj.GetComponent<PlatformEffector2D>() != null || obj.GetComponentInParent<DropThroughPlatform>() != null);

            if (airOnlyPlatform || dropPlatform)
            {
                NavMeshPlus.Components.NavMeshModifier airPlatformModifier = obj.GetComponent<NavMeshPlus.Components.NavMeshModifier>();
                if (airPlatformModifier == null)
                {
                    airPlatformModifier = obj.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
                }
                airPlatformModifier.overrideArea = false;
                airPlatformModifier.ignoreFromBuild = true;
                continue;
            }

            if ((obstacleLayer.value & (1 << obj.layer)) == 0) continue;
            if (obj.GetComponent<Collider2D>() == null) continue;

            NavMeshPlus.Components.NavMeshModifier modifier = obj.GetComponent<NavMeshPlus.Components.NavMeshModifier>();
            if (modifier == null)
            {
                modifier = obj.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            }
            modifier.overrideArea = true;
            modifier.area = 1;
        }
    }
}