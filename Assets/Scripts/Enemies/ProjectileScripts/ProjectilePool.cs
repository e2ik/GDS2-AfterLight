using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;

namespace Enemies.ProjectileScripts
{
    public static class ProjectilePool
    {
        private static readonly Dictionary<Projectile, ObjectPool<Projectile>> Pools = new();

        public static Projectile Get(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            var instance = GetOrCreatePool(prefab).Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public static void Release(Projectile prefab, Projectile instance)
        {
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
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p =>
                {
                    p.OnPoolRelease();
                    p.GameObject().SetActive(false);
                },
                actionOnDestroy: p => Object.Destroy(p.gameObject),
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 100);

            Pools[prefab] = pool;
            return pool;
        }
    }
}
