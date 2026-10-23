using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldStreamer : MonoBehaviour
{
    public static WorldStreamer Instance { get; private set; }

    public bool IsFastTraveling { get; set; } = false;
    public bool IsAligning { get; private set; } = false;
    private string currentLoadedStreamedScene;

    private HashSet<string> loadingScenes = new HashSet<string>();
    private readonly HashSet<string> unloadingScenes = new HashSet<string>();

    [SerializeField] private bool debugStreaming = false;

    [Header("Unload Safety")]
    [SerializeField] private bool unloadOnWebGL = false;
    [SerializeField, Min(0f)] private float unloadSafetyRadius = 4f;
    [SerializeField] private bool lowBackgroundLoadingPriority = true;
    private readonly List<Collider2D> safetyHits = new List<Collider2D>();

    public enum ResidentMode { Never, WebGLOnly, Always }

    [Header("Preload")]
    [SerializeField] private List<string> preloadScenes = new List<string>();
    [SerializeField] private ResidentMode keepScenesResident = ResidentMode.WebGLOnly;
    private readonly HashSet<string> residentScenes = new HashSet<string>();

    public bool UsesResidentScenes => keepScenesResident == ResidentMode.Always
                                      || (keepScenesResident == ResidentMode.WebGLOnly && Application.platform == RuntimePlatform.WebGLPlayer);

    public bool IsResident(string sceneName) => residentScenes.Contains(sceneName);

    public bool IsPreloading { get; private set; }

    public event Action<Scene> OnSceneStreamed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (lowBackgroundLoadingPriority) Application.backgroundLoadingPriority = ThreadPriority.Low;
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        residentScenes.Remove(scene.name);
    }

    public bool ActivateResident(string sceneName)
    {
        if (!residentScenes.Remove(sceneName)) return false;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded) return false;

        foreach (GameObject root in scene.GetRootGameObjects())
            root.SetActive(true);

        Log($"activated resident {sceneName}");
        return true;
    }

    public void MakeResident(Scene scene)
    {
        if (!scene.isLoaded || scene == SceneManager.GetActiveScene()) return;

        foreach (GameObject root in scene.GetRootGameObjects())
            root.SetActive(false);

        residentScenes.Add(scene.name);
        Log($"made {scene.name} resident");
    }

    public void StreamAndStitchScene(string sceneToLoad, string sourceAnchorID, string targetAnchorID)
    {
        if (IsFastTraveling) return;
        if (string.IsNullOrEmpty(sceneToLoad)) return;
        if (unloadingScenes.Contains(sceneToLoad) || loadingScenes.Contains(sceneToLoad)) return;

        if (residentScenes.Contains(sceneToLoad))
        {
            Log($"activating resident {sceneToLoad}");
            residentScenes.Remove(sceneToLoad);
            IsAligning = true;
            AlignAndActivate(SceneManager.GetSceneByName(sceneToLoad), sourceAnchorID, targetAnchorID);
            IsAligning = false;
            return;
        }

        if (SceneManager.GetSceneByName(sceneToLoad).isLoaded) return;

        Log($"loading {sceneToLoad}");
        StartCoroutine(StreamAndAlignRoutine(sceneToLoad, sourceAnchorID, targetAnchorID));
    }

    public void UnloadStreamedScene(string sceneName)
    {
        if (IsFastTraveling) return;
        if (string.IsNullOrEmpty(sceneName)) return;
        if (loadingScenes.Contains(sceneName) || unloadingScenes.Contains(sceneName) || residentScenes.Contains(sceneName)) return;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded || scene == SceneManager.GetActiveScene()) return;

        if (IsPlayerNearScene(scene))
        {
            Log($"kept {sceneName} loaded, player is standing near its geometry");
            return;
        }

        if (UsesResidentScenes)
        {
            MakeResident(scene);
            return;
        }

        if (Application.platform == RuntimePlatform.WebGLPlayer && !unloadOnWebGL) return;

        Log($"unloading {sceneName}");
        StartCoroutine(UnloadRoutine(sceneName));
    }

    private IEnumerator UnloadRoutine(string sceneName)
    {
        unloadingScenes.Add(sceneName);

        AsyncOperation unload = SceneManager.UnloadSceneAsync(sceneName);
        if (unload != null)
        {
            while (!unload.isDone) yield return null;
        }

        unloadingScenes.Remove(sceneName);
    }

    private IEnumerator StreamAndAlignRoutine(string sceneToLoad, string sourceAnchorID, string targetAnchorID)
    {
        IsAligning = true;
        loadingScenes.Add(sceneToLoad);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Scene newlyLoadedScene = SceneManager.GetSceneByName(sceneToLoad);
        AlignAndActivate(newlyLoadedScene, sourceAnchorID, targetAnchorID);

        loadingScenes.Remove(sceneToLoad);
        IsAligning = false;
    }

    private void AlignAndActivate(Scene newlyLoadedScene, string sourceAnchorID, string targetAnchorID)
    {
        GameObject[] rootObjects = newlyLoadedScene.GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            root.SetActive(false);
        }

        if (GameManager.Instance != null && newlyLoadedScene.IsValid())
        {
            AreaSide currentSide = GameManager.Instance.CurrentAreaSide;

            foreach (GameObject root in rootObjects)
            {
                SceneAreaState[] areaStates = root.GetComponentsInChildren<SceneAreaState>(true);
                foreach (var state in areaStates)
                {
                    state.SetSide(currentSide);
                }
            }
        }

        SceneAnchor sourceAnchor = FindAnchor(sourceAnchorID);
        SceneAnchor targetAnchor = FindAnchor(targetAnchorID);

        if (sourceAnchor != null && targetAnchor != null && targetAnchor.SceneRoot != null)
        {
            Vector3 positionOffset = sourceAnchor.transform.position - targetAnchor.transform.position;
            targetAnchor.SceneRoot.position += positionOffset;
        }
        else
        {
            Debug.LogWarning($"[WorldStreamer] Alignment failed. Source '{sourceAnchorID}' or Target '{targetAnchorID}' missing.");
        }

        foreach (GameObject root in rootObjects)
        {
            root.SetActive(true);
        }

        OnSceneStreamed?.Invoke(newlyLoadedScene);
    }

    public IEnumerator PreloadScenes()
    {
        if (preloadScenes == null || preloadScenes.Count == 0) yield break;

        IsPreloading = true;

        foreach (string sceneName in preloadScenes)
        {
            if (string.IsNullOrEmpty(sceneName)) continue;
            if (SceneManager.GetSceneByName(sceneName).isLoaded || loadingScenes.Contains(sceneName)) continue;

            Log($"preloading {sceneName}");

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null) continue;
            while (!load.isDone) yield return null;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) continue;

            MasterNavMeshController navMesh = MasterNavMeshController.Instance;
            if (UsesResidentScenes && navMesh != null && navMesh.CanPrebake)
            {
                float waitUntil = Time.realtimeSinceStartup + 10f;
                while (navMesh.IsBaking && Time.realtimeSinceStartup < waitUntil) yield return null;

                if (!navMesh.IsBaking)
                {
                    try { navMesh.PrebakeScene(scene); }
                    catch (System.Exception e) { Debug.LogException(e, this); }
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
                root.SetActive(false);

            if (UsesResidentScenes)
            {
                residentScenes.Add(sceneName);
                continue;
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload == null) continue;
            while (!unload.isDone) yield return null;
        }

        IsPreloading = false;
    }

    private bool IsPlayerNearScene(Scene scene)
    {
        if (unloadSafetyRadius <= 0f) return false;
        if (GameManager.Instance == null || GameManager.Instance.Player == null) return false;

        Vector2 center = GameManager.Instance.Player.transform.position;
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;

        int count = Physics2D.OverlapCircle(center, unloadSafetyRadius, filter, safetyHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = safetyHits[i];
            if (hit != null && hit.gameObject.scene == scene) return true;
        }

        return false;
    }

    private void Log(string message)
    {
        if (debugStreaming) Debug.Log($"[WorldStreamer] {message}", this);
    }

    private SceneAnchor FindAnchor(string anchorID)
    {
        SceneAnchor[] anchors = UnityEngine.Object.FindObjectsByType<SceneAnchor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var anchor in anchors)
        {
            if (anchor.AnchorID == anchorID) return anchor;
        }
        return null;
    }
}