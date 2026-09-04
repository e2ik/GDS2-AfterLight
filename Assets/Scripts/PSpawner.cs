using System.Collections.Generic;
using UnityEngine;

public class PSpawner : MonoBehaviour
{
    public static PSpawner Instance { get; private set; }

    [System.Serializable]
    public struct ParticleMapping
    {
        public string key;
        public ParticleSystem prefab;
        [Min(1)] public int initialPoolSize;
    }

    [SerializeField] private List<ParticleMapping> database = new List<ParticleMapping>();
    
    private Dictionary<string, Queue<ParticleSystem>> _pools;
    private Dictionary<string, ParticleSystem> _prefabs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Keeps the spawner and its pool alive across scene loads

        _pools = new Dictionary<string, Queue<ParticleSystem>>();
        _prefabs = new Dictionary<string, ParticleSystem>();

        foreach (var entry in database)
        {
            if (string.IsNullOrEmpty(entry.key) || entry.prefab == null) continue;

            _prefabs[entry.key] = entry.prefab;
            Queue<ParticleSystem> poolQueue = new Queue<ParticleSystem>();

            for (int i = 0; i < entry.initialPoolSize; i++)
            {
                ParticleSystem ps = CreatePooledInstance(entry.key, entry.prefab);
                poolQueue.Enqueue(ps);
            }

            _pools[entry.key] = poolQueue;
        }
    }

    private ParticleSystem CreatePooledInstance(string key, ParticleSystem prefab)
    {
        ParticleSystem ps = Instantiate(prefab, transform);
        ps.gameObject.SetActive(false);
        
        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Disable;
        
        return ps;
    }

    public static ParticleSystem Spawn(string key, Vector3 position, Quaternion? rotation = null)
    {
        ParticleSystem ps = GetPooledInstance(key);
        if (ps == null) return null;

        ps.transform.position = position;
        ps.transform.rotation = rotation ?? Quaternion.identity;
        ps.gameObject.SetActive(true);
        ps.Play(true);
        return ps;
    }

    // gracefully stops a particle system if it's playing, without destroying it
    public static void Stop(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private static ParticleSystem GetPooledInstance(string key)
    {
        if (Instance == null || !Instance._pools.ContainsKey(key))
        {
            Debug.LogWarning($"Particle effect '{key}' not found or PSpawner missing.");
            return null;
        }

        Queue<ParticleSystem> pool = Instance._pools[key];
        ParticleSystem ps = null;

        int checkedCount = 0;
        while (pool.Count > 0 && checkedCount < pool.Count)
        {
            var candidate = pool.Dequeue();
            pool.Enqueue(candidate);

            if (candidate != null && !candidate.gameObject.activeInHierarchy)
            {
                candidate.Clear(true);
                ps = candidate;
                break;
            }
            checkedCount++;
        }

        if (ps == null)
        {
            ps = Instance.CreatePooledInstance(key, Instance._prefabs[key]);
            pool.Enqueue(ps);
        }

        return ps;
    }
}