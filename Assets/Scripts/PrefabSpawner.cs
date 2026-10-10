using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Respawn")]
    [SerializeField] private bool respawnOnlyAfterRest = true;

    private static readonly HashSet<string> defeatedSpawners = new HashSet<string>();
    public static event Action OnDefeatedReset;

    private GameObject spawnedEnemy;
    private Enemies.EnemyHealth spawnedHealth;
    private Coroutine spawnCoroutine;
    private bool enemyAlive;
    private string spawnerKey;

    public GameObject SpawnedEnemy => spawnedEnemy;
    public bool IsDefeated => respawnOnlyAfterRest && defeatedSpawners.Contains(SpawnerKey);

    private string SpawnerKey
    {
        get
        {
            if (string.IsNullOrEmpty(spawnerKey))
            {
                Vector3 p = transform.position;
                spawnerKey = $"{gameObject.scene.name}:{Mathf.RoundToInt(p.x * 100f)}:{Mathf.RoundToInt(p.y * 100f)}:{targetAreaSide}";
            }
            return spawnerKey;
        }
    }

    public static void ResetAllDefeated()
    {
        defeatedSpawners.Clear();
        OnDefeatedReset?.Invoke();
    }

    public static void ClearDefeated()
    {
        defeatedSpawners.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        defeatedSpawners.Clear();
        OnDefeatedReset = null;
    }

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

    private void OnEnable()
    {
        OnDefeatedReset += HandleDefeatedReset;
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
        if (enemyAlive && spawnedEnemy == null)
            HandleEnemyKilled();

        if (GameManager.Instance == null) return;

        bool isCurrentArea = GameManager.Instance.CurrentAreaSide == targetAreaSide;

        if (!isCurrentArea && spawnedEnemy != null)
        {
            DespawnEnemy();
        }
        else if (isCurrentArea && spawnedEnemy == null && spawnCoroutine == null && !IsDefeated)
        {
            TrySpawn();
        }
    }

    private void HandleEnemyKilled()
    {
        enemyAlive = false;
        UnhookHealth();
        if (respawnOnlyAfterRest) defeatedSpawners.Add(SpawnerKey);
    }

    private void UnhookHealth()
    {
        if (spawnedHealth != null) spawnedHealth.OnDeath -= HandleEnemyKilled;
        spawnedHealth = null;
    }

    private void HandleDefeatedReset()
    {
        if (spawnedEnemy != null && spawnedEnemy.activeSelf && enemyAlive) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentAreaSide != targetAreaSide) return;

        DespawnEnemy();
        TrySpawn();
    }

    public void ForceRespawn()
    {
        defeatedSpawners.Remove(SpawnerKey);
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
        if (IsDefeated) return;

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

        enemyAlive = false;
        UnhookHealth();

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

        if (GameManager.Instance.CurrentAreaSide != targetAreaSide || IsDefeated)
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
        enemyAlive = true;

        spawnedHealth = spawnedEnemy.GetComponentInChildren<Enemies.EnemyHealth>(true);
        if (spawnedHealth != null) spawnedHealth.OnDeath += HandleEnemyKilled;

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
        OnDefeatedReset -= HandleDefeatedReset;
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