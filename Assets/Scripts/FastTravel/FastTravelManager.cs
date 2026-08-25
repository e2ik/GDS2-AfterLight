using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FastTravelManager : MonoBehaviour
{
    public static FastTravelManager Instance { get; private set; }

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

    public void TravelTo(FastTravelNodeSO node)
    {
        if (node == null) return;
        StartCoroutine(FastTravelRoutine(node));
    }

    private IEnumerator FastTravelRoutine(FastTravelNodeSO destination)
    {
        string targetScene = destination.targetSceneName;

        Scene masterScene = SceneManager.GetSceneByName("WorldMaster");
        if (masterScene.isLoaded)
        {
            SceneManager.SetActiveScene(masterScene);
        }

        if (!SceneManager.GetSceneByName(targetScene).isLoaded)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }

        List<Scene> scenesToUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != "WorldMaster" && scene.name != targetScene && scene.isLoaded)
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

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Transform anchorTransform = FindAnchorTransform(destination.spawnAnchorID);

            if (anchorTransform != null)
            {
                player.transform.position = anchorTransform.position;
            }
            else
            {
                Debug.LogWarning($"[FastTravelManager] Could not find spawn anchor '{destination.spawnAnchorID}' inside scene '{targetScene}'.");
            }
        }
    }

    private Transform FindAnchorTransform(string anchorID)
    {
        FastTravelSpawnAnchor[] anchors = Object.FindObjectsByType<FastTravelSpawnAnchor>(FindObjectsSortMode.None);
        foreach (var anchor in anchors)
        {
            if (anchor.AnchorID == anchorID)
            {
                return anchor.transform;
            }
        }
        return null;
    }
}