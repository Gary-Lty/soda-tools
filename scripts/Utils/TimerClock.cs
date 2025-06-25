using System;
using System.Timers;
using UniRx;
using UnityEngine;
    /// <summary>
    /// 倒计时闹钟
    /// </summary>
    public class TimerClock
    {
        private Action _callback;
        private readonly float _maxTime;
        private float _timer;
        private IDisposable _disposable;
        private bool _isPause;

        public static TimerClock Setup(float second,Action onTimeEnd)
        {
            var clock = new TimerClock(second);
            clock.Bind(onTimeEnd);
            return clock;
        }

        public TimerClock(float seconds)
        {
            _maxTime = seconds;
        }

        void Tick(long _)
        {
            if(_isPause) return;
            _timer += Time.deltaTime;
            if (_timer > _maxTime)
            {
                OnTimeEnd();
            }
        }

        private void OnTimeEnd()
        {
            _timer = 0;
            _disposable?.Dispose();
            _callback?.Invoke();
        }

        /// <summary>
        /// 开始倒计时，并绑定
        /// </summary>
        /// <param name="bind"></param>
        /// <returns></returns>
        public IDisposable Start(GameObject bind)
        {
            _disposable?.Dispose();
            _disposable = Observable.EveryUpdate().Subscribe(Tick);
            _disposable.AddTo(bind);
            return _disposable;
        }

        public void Bind(Action onTimeEnd)
        {
            this._callback = onTimeEnd;
        }

        public void Pause()
        {
            _isPause = true;
        }

        public void Resume()
        {
            _isPause = false;
        }
        
        
    }
