using System;
using UnityEngine;

namespace HCR.Manager
{
    public class AudioClipObject : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        private void OnEnable()
        {
            Invoke(nameof(Release), 5);
        }

        void Release()
        {
            AudioSourcePool.instance.Push(this.source);
        }
    }
}