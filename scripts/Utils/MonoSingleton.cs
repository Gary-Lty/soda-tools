using System;
using UnityEngine;

namespace HCR.Manager
{
    public class MonoSingleton<T> : MonoBehaviour where T : class
    {
        public static T instance => _instance;
        private static T _instance;

        protected virtual void Awake()
        {
            _instance = this as T;
        }
    }
}