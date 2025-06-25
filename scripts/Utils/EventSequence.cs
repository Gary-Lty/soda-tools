using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;



    public class EventSequence
    {
        class Event
        {
            public float time;
            public Action action;
            private bool hasTrigger;

            public bool HasTrigger => hasTrigger;

            public void Trigger()
            {
                hasTrigger = true;
                action?.Invoke();
            }
            
        }
        
        List<Event> _eventList = new List<Event>();
        private float _timer;

        public Action OnComplete;
        private bool _isPause;
        private UniRx.CompositeDisposable _ticker = new();

        public void AddEvent(float time, Action action)
        {
            this._eventList.Add(new Event { time = time, action = action });
        }

        void Tick(long _)
        {
            if(_isPause) return;
            _timer += Time.deltaTime;
            for (var i = _eventList.Count - 1; i >= 0; i--)
            {
                var evt = _eventList[i];
                if (!evt.HasTrigger && evt.time <= _timer)
                {
                    evt.Trigger();
                }
            }
            
            bool allTrigger = _eventList.All(e=>e.HasTrigger);

            if (allTrigger)
            {
                OnComplete?.Invoke();
                this.Dispose();
            }
        }

        private void Dispose()
        {
            _ticker.Dispose();
        }

        public static EventSequence StartOn(GameObject addTo)
        {
            var sequence = new EventSequence();
            return sequence.Start(addTo);
        }

        public EventSequence Start(GameObject addTo)
        {
           var update =  Observable.EveryUpdate().Subscribe(Tick).AddTo(addTo);
           _ticker.Add(update);
           return this;
        }

        public EventSequence ListenPauseEvent()
        {
            //var pause = MessageBroker.Default.Receive<LevelPause>().Subscribe();
            //_ticker.Add(pause);
            return this;
        }

        void OnLevelPause(LevelPause evt)
        {
            _isPause = evt.isOn;
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
