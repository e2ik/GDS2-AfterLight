using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FastTravelManager : MonoBehaviour
{
    public static FastTravelManager Instance { get; private set; }

    private FastTravelNodeSO lastVisitedNode;
    private AreaSide lastVisitedSide;

    public bool HasLastVisitedNode => lastVisitedNode != null;
    public event System.Action OnFastTravelComplete;

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

    public void SetLastInteractedNode(FastTravelNodeSO node)
    {
        if (node == null) return;

        AreaSide sideAtInteract = GameManager.Instance != null ? GameManager.Instance.CurrentAreaSide : AreaSide.Exterior;

        lastVisitedNode = node;
        lastVisitedSide = sideAtInteract;

        if (SaveManager.Instance != null)
        {
            var saveData = SaveManager.Instance.GetSaveData();
            if (saveData?.progress != null)
            {
                saveData.progress.lastInteractedFastTravelID = node.nodeID;
                saveData.progress.lastAreaSide = sideAtInteract;
            }
        }
    }

    public void TravelTo(FastTravelNodeSO node)
    {
        if (node == null) return;

        AreaSide sideAtInteract = GameManager.Instance != null ? GameManager.Instance.CurrentAreaSide : AreaSide.Exterior;
        SetLastInteractedNode(node);

        StartCoroutine(FastTravelRoutine(node, sideAtInteract));
    }

    public void RespawnAtLastFastTravel()
    {
        if (lastVisitedNode == null)
        {
            Debug.LogWarning("[FastTravelManager] No fast travel point visited this session; cannot respawn.");
            OnFastTravelComplete?.Invoke();
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[FastTravelManager] GameManager.Instance missing — cannot respawn.");
            OnFastTravelComplete?.Invoke();
            return;
        }

        GameManager.Instance.ForceReloadAndRespawn(lastVisitedNode, lastVisitedSide, () => OnFastTravelComplete?.Invoke());
    }

    private IEnumerator FastTravelRoutine(FastTravelNodeSO destination, AreaSide sideAtInteract)
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
            yield return null;
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
            GameManager.Instance?.ApplyAreaSide(sideAtInteract);

            Transform anchorTransform = FindAnchorTransform(destination.spawnAnchorID);

            if (anchorTransform != null)
            {
                player.transform.position = anchorTransform.position;

                CameraFollow2D cam = FindFirstObjectByType<CameraFollow2D>();
                cam?.SnapToTarget();

                SaveManager.Instance?.SaveProgressAtLocation(targetScene, destination.spawnAnchorID, sideAtInteract);
            }
            else
            {
                Debug.LogWarning($"[FastTravelManager] Could not find spawn anchor '{destination.spawnAnchorID}' inside scene '{targetScene}'.");
            }
        }
        else
        {
            Debug.LogWarning("[FastTravelManager] No GameObject tagged 'Player' found — cannot reposition on respawn.");
        }

        OnFastTravelComplete?.Invoke();
    }

    public Transform FindAnchorTransform(string anchorID)
    {
        FastTravelSpawnAnchor[] anchors = Object.FindObjectsByType<FastTravelSpawnAnchor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var anchor in anchors)
        {
            if (anchor.AnchorID == anchorID)
            {
                anchor.gameObject.SetActive(true);
                return anchor.transform;
            }
        }

        Debug.LogWarning($"[FastTravelManager] Spawn anchor '{anchorID}' could not be found among {anchors.Length} total anchors.");
        return null;
    }
}