using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FastTravelManager : MonoBehaviour
{
    public static FastTravelManager Instance { get; private set; }

    private string currentLoadedZone;

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

    private void Start()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != gameObject.scene.name)
            {
                currentLoadedZone = scene.name;
                break;
            }
        }
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

        if (!string.IsNullOrEmpty(currentLoadedZone) && currentLoadedZone != targetScene && currentLoadedZone != "WorldMaster")
        {
            Scene sceneToUnload = SceneManager.GetSceneByName(currentLoadedZone);
            if (sceneToUnload.isLoaded && sceneToUnload.name != "WorldMaster")
            {
                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(currentLoadedZone);
                if (asyncUnload != null)
                {
                    while (!asyncUnload.isDone)
                    {
                        yield return null;
                    }
                }
            }
        }

        currentLoadedZone = targetScene;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            player.transform.position = destination.targetWorldPosition;
        }
    }
}