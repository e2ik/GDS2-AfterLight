using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(SpriteRenderer))]
public class PrefabSpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Area Configuration")]
    [SerializeField] private AreaSide targetAreaSide = AreaSide.Exterior;

    private GameObject spawnedEnemy;
    private Coroutine spawnCoroutine;

    private void Awake()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[PrefabSpawner] No prefab assigned on {gameObject.name}");
        }
        SpriteRenderer previewRenderer = GetComponent<SpriteRenderer>();
        if (previewRenderer != null)
        {
            previewRenderer.sprite = null;
        }
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            TrySpawn();
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool isCurrentArea = GameManager.Instance.CurrentAreaSide == targetAreaSide;

        if (!isCurrentArea && spawnedEnemy != null)
        {
            DespawnEnemy();
        }
        else if (isCurrentArea && spawnedEnemy == null && spawnCoroutine == null)
        {
            TrySpawn();
        }
    }

    public void ForceRespawn()
    {
        DespawnEnemy();
        TrySpawn();
    }

    public void TrySpawn()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[PrefabSpawner] No prefab assigned on {gameObject.name}");
            return;
        }

        if (spawnedEnemy != null) return;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        spawnCoroutine = StartCoroutine(WaitAndSpawnRoutine());
    }

    public void DespawnEnemy()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (spawnedEnemy != null)
        {
            Destroy(spawnedEnemy);
            spawnedEnemy = null;
        }
    }

    private IEnumerator WaitAndSpawnRoutine()
    {
        while (GameManager.Instance == null)
        {
            yield return null;
        }

        if (WorldStreamer.Instance != null)
        {
            while (WorldStreamer.Instance.IsAligning)
            {
                yield return null;
            }
        }

        if (GameManager.Instance.CurrentAreaSide != targetAreaSide)
        {
            spawnCoroutine = null;
            yield break;
        }

        if (!gameObject.scene.isLoaded)
        {
            spawnCoroutine = null;
            yield break;
        }

        spawnedEnemy = Instantiate(prefabToSpawn, transform.position, transform.rotation);

        Vector3 pos = spawnedEnemy.transform.position;
        spawnedEnemy.transform.position = new Vector3(pos.x, pos.y, 0f);

        Scene masterScene = SceneManager.GetActiveScene();
        if (spawnedEnemy.scene != masterScene)
        {
            SceneManager.MoveGameObjectToScene(spawnedEnemy, masterScene);
        }

        spawnCoroutine = null;
    }

    private void OnDisable()
    {
        DespawnEnemy();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SpriteRenderer previewRenderer = GetComponent<SpriteRenderer>();
        if (previewRenderer == null) return;

        if (prefabToSpawn == null)
        {
            previewRenderer.sprite = null;
            return;
        }

        SpriteRenderer prefabSr = prefabToSpawn.GetComponentInChildren<SpriteRenderer>();
        previewRenderer.sprite = prefabSr != null ? prefabSr.sprite : null;
    }
#endif
}