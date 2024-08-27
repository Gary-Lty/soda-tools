using System.Collections.Generic;
using UnityEngine;

namespace HCR.Manager
{
    [System.Serializable]
    public class AudioContainer
    {
        public List<AudioClip> clips;

        public void Play()
        {
            var clip = GameManager.GetRandomItem(clips);
            var source = AudioSourcePool.instance.Get(clip);
            if (source)
            {
                source.Play();
            }
        }
    }
}