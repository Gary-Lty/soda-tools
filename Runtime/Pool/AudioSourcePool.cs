using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace HCR.Manager
{
    public class AudioSourcePool : MonoInstance<AudioSourcePool>
    {
        public GameObject audioSourcePrefab;
        private Dictionary<AudioClip, AudioPool> _dict = new();

        public AudioSource Get(AudioClip clip)
        {
            if (!clip)
            {
                Debug.LogException(new NullReferenceException());
            }
            if (!_dict.TryGetValue(clip, out var pool))
            {
                pool = new AudioPool(() =>
                    {
                        var item = Instantiate(audioSourcePrefab, transform);
                        item.transform.SetParent(transform);
                        return item.GetComponent<AudioSource>();
                    },
                    v => v.gameObject.SetActive(true),
                    v => v.gameObject.SetActive(false)
                );
                _dict.Add(clip,pool);
            }
            var source = pool.Get();
            source.clip = clip;
            return source;
        }

        public AudioSource GetAt(AudioClip clip, Vector3 position)
        {
            if (!clip)
            {
                Debug.LogException(new NullReferenceException());
                return null;
            }
            if (!_dict.TryGetValue(clip, out var pool))
            {
                pool = new AudioPool(() =>
                    {
                        var item = Instantiate(audioSourcePrefab, transform);
                        item.transform.SetParent(transform);
                        return item.GetComponent<AudioSource>();
                    },
                    v => v.gameObject.SetActive(true),
                    v => v.gameObject.SetActive(false)
                );
                _dict.Add(clip,pool);
            }

            var source = pool.Get();
            return source;
        }

        public void Push(AudioSource source)
        {
            if (_dict.TryGetValue(source.clip, out var pool))
            {
                pool.Release(source);
            }
        }
    }

    public class AudioPool : ObjectPool<AudioSource>
    {
        public AudioPool(Func<AudioSource> createFunc,
            Action<AudioSource> actionOnGet = null,
            Action<AudioSource> actionOnRelease = null,
            Action<AudioSource> actionOnDestroy = null, bool collectionCheck = true, int defaultCapacity = 10,
            int maxSize = 10000)
            : base(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck, defaultCapacity, maxSize)
        {
        }
    }
}