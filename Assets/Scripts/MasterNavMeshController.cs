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

    private Scene currentScene;
    private bool hasCurrentScene;
    private bool dirty;
    private Transform playerTransform;

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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAreaSideChanged += HandleAreaSideChanged;
        }

        if (WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.OnSceneStreamed += HandleSceneStreamed;
        }
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAreaSideChanged -= HandleAreaSideChanged;
        }

        if (WorldStreamer.Instance != null)
        {
            WorldStreamer.Instance.OnSceneStreamed -= HandleSceneStreamed;
        }
    }

    private void HandleSceneStreamed(Scene streamedScene)
    {
        SetActiveScene(streamedScene);
    }

    void Update()
    {
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
            if (detectedScene.IsValid() && (!hasCurrentScene || detectedScene != currentScene))
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
        return currentScene;
    }

    public void SetActiveScene(Scene scene)
    {
        if (hasCurrentScene && scene == currentScene) return;

        currentScene = scene;
        hasCurrentScene = true;
        dirty = true;
    }

    void LateUpdate()
    {
        if (dirty && hasCurrentScene)
        {
            ProcessLayerAndBake(currentScene);
            dirty = false;
        }
    }

    private void HandleAreaSideChanged(AreaSide areaSide)
    {
        dirty = true;
    }

    private void ProcessLayerAndBake(Scene scene)
    {
        if (!scene.IsValid())
        {
            Debug.LogWarning("[MasterNavMeshController] Tried to bake an invalid scene.");
            return;
        }

        if (playerTransform != null && navMeshSurface != null)
        {
            navMeshSurface.transform.position = playerTransform.position;
        }

        List<GameObject> sceneObjects = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            sceneObjects.Add(root);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                sceneObjects.Add(child.gameObject);
            }
        }

        foreach (GameObject obj in sceneObjects)
        {
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

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
        else
        {
            Debug.LogWarning("[MasterNavMeshController] NavMeshSurface reference is missing.");
        }
    }
}