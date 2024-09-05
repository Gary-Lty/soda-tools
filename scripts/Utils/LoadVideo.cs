using System;
using UnityEngine;
using UnityEngine.Video;

namespace Utils
{
    public class LoadVideo : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        public string videoLocalPath;
        private bool _hasStart;

        private void OnEnable()
        {
            _hasStart = false;
            videoPlayer.url = Application.streamingAssetsPath+"/"+videoLocalPath;
            videoPlayer.started += source => this._hasStart = true;
            videoPlayer.errorReceived += (source, message) => gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_hasStart && !videoPlayer.isPlaying)
            {
                gameObject.SetActive(false);
            }
        }
    }
}