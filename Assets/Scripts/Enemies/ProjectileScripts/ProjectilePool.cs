using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Enemies.ProjectileScripts
{
    public static class ProjectilePool
    {
        private static readonly Dictionary<Projectile, ObjectPool<Projectile>> Pools = new();

        public static Projectile Get(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            var pool = GetOrCreatePool(prefab);
            var instance = pool.Get();

            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                instance.SourcePrefab = prefab;
                instance.gameObject.SetActive(true);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public static void Release(Projectile prefab, Projectile instance)
        {
            if (instance == null) return;

            if (prefab == null)
            {
                Object.Destroy(instance.gameObject);
                return;
            }
            
            GetOrCreatePool(prefab).Release(instance);
        }

        private static ObjectPool<Projectile> GetOrCreatePool(Projectile prefab)
        {
            if (Pools.TryGetValue(prefab, out var pool))
                return pool;

            pool = new ObjectPool<Projectile>(
                createFunc: () =>
                {
                    var instance = Object.Instantiate(prefab);
                    instance.SourcePrefab = prefab;
                    return instance;
                },
                actionOnGet: p =>
                {
                    if (p != null) p.gameObject.SetActive(true);
                },
                actionOnRelease: p =>
                {
                    if (p == null) return;
                    p.OnPoolRelease();
                    p.gameObject.SetActive(false);
                },
                actionOnDestroy: p =>
                {
                    if (p != null) Object.Destroy(p.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 100);

            Pools[prefab] = pool;
            return pool;
        }
    }
}