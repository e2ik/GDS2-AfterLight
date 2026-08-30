using System.Collections;
using UnityEngine;

public class PrefabSpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private bool spawnOnStart = true;

    private GameObject spawnedEnemy;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnEnemy();
        }
    }

    public void SpawnEnemy()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[PrefabSpawner] No prefab assigned on {gameObject.name}");
            return;
        }

        if (spawnedEnemy != null) return;

        StartCoroutine(WaitAndSpawnRoutine());
    }

    private IEnumerator WaitAndSpawnRoutine()
    {
        if (WorldStreamer.Instance != null)
        {
            while (WorldStreamer.Instance.IsAligning)
            {
                yield return null;
            }
        }

        spawnedEnemy = Instantiate(prefabToSpawn, transform.position, transform.rotation);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying && prefabToSpawn != null)
        {
            SpriteRenderer sr = prefabToSpawn.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Sprite sprite = sr.sprite;
                float width = (sprite.rect.width / sprite.pixelsPerUnit) * transform.localScale.x;
                float height = (sprite.rect.height / sprite.pixelsPerUnit) * transform.localScale.y;
                Rect spriteRect = new Rect(
                    transform.position.x - (width / 2f),
                    transform.position.y - (height / 2f),
                    width,
                    height
                );

                Gizmos.DrawGUITexture(spriteRect, sprite.texture);
                return;
            }
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}