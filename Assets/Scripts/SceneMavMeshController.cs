using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SceneNavMeshController : MonoBehaviour
{
    [SerializeField] private NavMeshPlus.Components.NavMeshSurface navMeshSurface;
    [SerializeField] private LayerMask obstacleLayer;

    void Start()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshPlus.Components.NavMeshSurface>();
            if (navMeshSurface == null)
            {
                Debug.LogWarning("NavMeshSurface reference is missing on the controller.");
            }
        } else {
            ProcessLayerAndBake();
        }
    }

    void OnEnable()
    {
        ProcessLayerAndBake();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAreaSideChanged += ProcessLayerAndBake;
        }
    }

    void OnDisable()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.RemoveData();
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAreaSideChanged -= ProcessLayerAndBake;
        }
    }

    public void ProcessLayerAndBake()
    {
        ProcessLayerAndBake(default);
    }

    public void ProcessLayerAndBake(AreaSide areaSide)
    {
        // Restrict search to ONLY this specific loaded scene
        Scene currentScene = gameObject.scene;
        GameObject[] rootObjects = currentScene.GetRootGameObjects();
        
        List<GameObject> sceneObjects = new List<GameObject>();
        foreach (GameObject root in rootObjects)
        {
            sceneObjects.Add(root);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                sceneObjects.Add(child.gameObject);
            }
        }

        foreach (GameObject obj in sceneObjects)
        {
            if ((obstacleLayer.value & (1 << obj.layer)) != 0)
            {
                if (obj.GetComponent<Collider2D>() == null)
                {
                    continue;
                }

                NavMeshPlus.Components.NavMeshModifier modifier = obj.GetComponent<NavMeshPlus.Components.NavMeshModifier>();
                if (modifier == null)
                {
                    modifier = obj.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
                }
                modifier.overrideArea = true;
                modifier.area = 1;
            }
        }

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
        else
        {
            Debug.LogWarning("NavMeshSurface reference is missing on the controller.");
        }
    }
}