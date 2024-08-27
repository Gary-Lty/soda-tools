using System;
using System.Collections.Generic;
using UnityEngine;

namespace HCR.Manager
{
    public class EffectPoolManager : MonoInstance<EffectPoolManager>
    {
        public Dictionary<string, FxPoolFacade> allFxPool = new();

        public GameObject PlayAt(GameObject fxPrefab, Vector3 position, Quaternion quaternion)
        {
            var label = fxPrefab.GetComponent<EffectPoolObject>();
            if (!label)
            {
                throw new NullReferenceException("PlayAt特效没有回收标签");
            }

            if (!allFxPool.TryGetValue(label.GUID, out var facade))
            {
                facade = new FxPoolFacade(fxPrefab);
                allFxPool.Add(label.GUID, facade);
            }

            var fxObject = facade.pool.Get();
            //特效在前面播放，防止被遮挡
            fxObject.transform.SetPositionAndRotation(position-Vector3.forward, quaternion);
            return fxObject;
        }

        public void Release(string guid, GameObject fxObject)
        {
            if (!allFxPool.TryGetValue(guid, out var facade))
            {
                throw new NullReferenceException("Release特效没有Pool回收");
            }

            facade.pool.Release(fxObject);
        }

        public void Release(GameObject fxObject)
        {
            var label = fxObject.GetComponent<EffectPoolObject>();
            if (!label)
            {
                throw new NullReferenceException("Release特效没有回收标签");
            }

            Release(label.GUID, fxObject);
        }
    }

    public class FxPoolFacade
    {
        public GameObject prefab;
        public FxPool pool;

        public FxPoolFacade(GameObject prefab)
        {
            this.prefab = prefab;
            pool = new FxPool(OnCreate, OnGetFx, OnRelease, actionOnDestroy);
        }

        GameObject OnCreate()
        {
            return GameObject.Instantiate(this.prefab);
        }

        private void OnGetFx(GameObject fxObject)
        {
            fxObject.SetActive(true);
        }

        private void OnRelease(GameObject fxObject)
        {
            fxObject.SetActive(false);
        }

        void actionOnDestroy(GameObject fxObject)
        {
            GameObject.Destroy(fxObject);
        }
    }

    public class FxPool : UnityEngine.Pool.ObjectPool<GameObject>
    {
        public FxPool(Func<GameObject> createFunc,
            Action<GameObject> actionOnGet = null,
            Action<GameObject> actionOnRelease = null,
            Action<GameObject> actionOnDestroy = null,
            bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
            : base(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck, defaultCapacity, maxSize)
        {
        }
    }
}