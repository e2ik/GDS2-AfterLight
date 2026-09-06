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
        if (SceneManager.GetSceneByName(sceneToLoad).isLoaded || loadingScenes.Contains(sceneToLoad)) return;

        StartCoroutine(StreamAndAlignRoutine(sceneToLoad, sourceAnchorID, targetAnchorID));
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