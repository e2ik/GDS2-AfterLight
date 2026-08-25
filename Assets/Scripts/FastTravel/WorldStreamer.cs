using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldStreamer : MonoBehaviour
{
    public static WorldStreamer Instance { get; private set; }

    public bool IsFastTraveling { get; set; } = false;

    private HashSet<string> loadingScenes = new HashSet<string>();

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
        loadingScenes.Add(sceneToLoad);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        while (!asyncLoad.isDone) yield return null;

        SceneAnchor sourceAnchor = FindAnchor(sourceAnchorID);
        SceneAnchor targetAnchor = FindAnchor(targetAnchorID);

        if (sourceAnchor != null && targetAnchor != null && targetAnchor.SceneRoot != null)
        {
            Vector3 positionOffset = sourceAnchor.transform.position - targetAnchor.transform.position;
            targetAnchor.SceneRoot.position += positionOffset;
        }

        loadingScenes.Remove(sceneToLoad);
    }

    private SceneAnchor FindAnchor(string anchorID)
    {
        SceneAnchor[] anchors = Object.FindObjectsByType<SceneAnchor>(FindObjectsSortMode.None);
        foreach (var anchor in anchors)
        {
            if (anchor.AnchorID == anchorID) return anchor;
        }
        return null;
    }
}