using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldStreamer : MonoBehaviour
{
    public static WorldStreamer Instance { get; private set; }

    private HashSet<string> loadedScenes = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            loadedScenes.Add(SceneManager.GetSceneAt(i).name);
        }
    }

    public void LoadZoneAdditive(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || loadedScenes.Contains(sceneName)) 
            return;

        StartCoroutine(LoadAdditiveRoutine(sceneName));
    }

    public void UnloadZoneAdditive(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !loadedScenes.Contains(sceneName)) 
            return;

        StartCoroutine(UnloadAdditiveRoutine(sceneName));
    }

    private IEnumerator LoadAdditiveRoutine(string sceneName)
    {
        loadedScenes.Add(sceneName);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    private IEnumerator UnloadAdditiveRoutine(string sceneName)
    {
        loadedScenes.Remove(sceneName);

        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);
        if (asyncUnload == null) yield break;

        while (!asyncUnload.isDone)
        {
            yield return null;
        }
    }
}