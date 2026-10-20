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

    [Header("Preload")]
    [SerializeField] private List<string> preloadScenes = new List<string>();

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
    }

    public void StreamAndStitchScene(string sceneToLoad, string sourceAnchorID, string targetAnchorID)
    {
        if (IsFastTraveling) return;
        if (string.IsNullOrEmpty(sceneToLoad)) return;
        if (unloadingScenes.Contains(sceneToLoad)) return;
        if (SceneManager.GetSceneByName(sceneToLoad).isLoaded || loadingScenes.Contains(sceneToLoad)) return;

        Log($"loading {sceneToLoad}");
        StartCoroutine(StreamAndAlignRoutine(sceneToLoad, sourceAnchorID, targetAnchorID));
    }

    public void UnloadStreamedScene(string sceneName)
    {
        if (IsFastTraveling) return;
        if (string.IsNullOrEmpty(sceneName)) return;
        if (loadingScenes.Contains(sceneName) || unloadingScenes.Contains(sceneName)) return;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded || scene == SceneManager.GetActiveScene()) return;

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

        loadingScenes.Remove(sceneToLoad);
        IsAligning = false;

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

            foreach (GameObject root in scene.GetRootGameObjects())
                root.SetActive(false);

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload == null) continue;
            while (!unload.isDone) yield return null;
        }

        IsPreloading = false;
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