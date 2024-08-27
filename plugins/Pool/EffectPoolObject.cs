using System;
using UnityEngine;

namespace HCR.Manager
{
    /// <summary>
    /// 特效对象的回收标签
    /// </summary>
    public class EffectPoolObject : MonoBehaviour
    {
        [SerializeField]
        private string guid;
        public string GUID => guid;

        public float releaseInTime = 2;
        private float _time = 0;

#if UNITY_EDITOR
        
        private void Reset()
        {
            this.guid = UnityEditor.AssetDatabase.GetAssetPath(this.gameObject);
            this.guid = UnityEditor.AssetDatabase.AssetPathToGUID(guid);
        }
#endif

        private void OnEnable()
        {
            _time = 0;
        }

        private void Update()
        {
            if (_time <= releaseInTime)
            {
                _time += Time.deltaTime;
            }
            EffectPoolManager.instance.Release(GUID,gameObject);
        }
    }
}