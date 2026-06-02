using System.Collections.Generic;
using UnityEngine;

namespace PBS2D
{
    public enum PoolType
    {
        None,
        Unparented,
        SoundPlayer,
        Projectile,
        Effect,
        Casing
    }

    public class ObjectPoolManager : MonoBehaviour
    {
        public static Dictionary<string, PooledObjectInfo> ObjectPools = new();

        private GameObject _objectPoolEmptyHolder;

        private static GameObject _soundPlayersEmpty;
        private static GameObject _projectilesEmpty;
        private static GameObject _effectsEmpty;
        private static GameObject _casingsEmpty;
        private static GameObject _genericEmpty;

        private void Awake()
        {
            ObjectPools.Clear();
            SetupEmpties();
        }

        private void SetupEmpties()
        {
            _objectPoolEmptyHolder = new GameObject("Pooled Objects");

            _soundPlayersEmpty = new GameObject("Sound Players");
            _soundPlayersEmpty.transform.SetParent(_objectPoolEmptyHolder.transform);

            _projectilesEmpty = new GameObject("Projectiles");
            _projectilesEmpty.transform.SetParent(_objectPoolEmptyHolder.transform);

            _effectsEmpty = new GameObject("Effects");
            _effectsEmpty.transform.SetParent(_objectPoolEmptyHolder.transform);

            _casingsEmpty = new GameObject("Casings");
            _casingsEmpty.transform.SetParent(_objectPoolEmptyHolder.transform);

            _genericEmpty = new GameObject("Generic");
            _genericEmpty.transform.SetParent(_objectPoolEmptyHolder.transform);
        }

        public static GameObject SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot, PoolType poolType = PoolType.None)
        {
            if (!ObjectPools.TryGetValue(prefab.name, out PooledObjectInfo pool))
            {
                pool = new PooledObjectInfo();
                ObjectPools.Add(prefab.name, pool);
            }

            GameObject spawnedObj;

            if (pool.inactiveObjects.Count > 0)
            {
                spawnedObj = pool.inactiveObjects.Pop();
                spawnedObj.transform.SetPositionAndRotation(pos, rot);
                spawnedObj.SetActive(true);
            }
            else
            {
                spawnedObj = Instantiate(prefab, pos, rot);

                spawnedObj.name = prefab.name;

                if (poolType != PoolType.Unparented)
                {
                    GameObject parent = GetParentForType(poolType);
                    if (parent != null) spawnedObj.transform.SetParent(parent.transform);
                }
            }

            return spawnedObj;
        }

        public static void ReturnObjectToPool(GameObject obj)
        {
            if (ObjectPools.TryGetValue(obj.name, out PooledObjectInfo pool))
            {
                obj.SetActive(false);
                pool.inactiveObjects.Push(obj);
            }
            else
            {
                Debug.LogWarning($"Trying to release an object that is not managed by the pool: {obj.name}. Destroying instead.");
                Destroy(obj);
            }
        }

        public static Transform GetPoolParent(PoolType poolType)
        {
            GameObject parent = GetParentForType(poolType);
            return parent != null ? parent.transform : null;
        }

        private static GameObject GetParentForType(PoolType poolType)
        {
            return poolType switch
            {
                PoolType.SoundPlayer => _soundPlayersEmpty,
                PoolType.Projectile => _projectilesEmpty,
                PoolType.Effect => _effectsEmpty,
                PoolType.Casing => _casingsEmpty,
                PoolType.None => _genericEmpty,
                _ => null
            };
        }
    }

    public class PooledObjectInfo
    {
        public Stack<GameObject> inactiveObjects = new();
    }
}
