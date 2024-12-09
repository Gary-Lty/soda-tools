using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gary.UIManagement
{
    public class ContextManager
    {
        /// <summary>
        /// canvas 堆栈中的界面
        /// </summary>
        public static readonly Stack<IContext> container = new Stack<IContext>();

        /// <summary>
        /// 已经加载并完成初始化的界面
        /// </summary>
        public static readonly Dictionary<Type, IContext> contextDict = new Dictionary<Type, IContext>();

        /// <summary>
        /// 已经加载的预制体资源
        /// </summary>
        private static readonly Dictionary<string, GameObject> _viewDict = new();

        static Transform _canvasRoot;

        public static Transform CanvasRoot
        {
            get
            {
                if (!_canvasRoot)
                {
                    _canvasRoot = GameObject.Find("Canvas").transform;
                }

                return _canvasRoot;
            }
        }

        public static void Init(Transform canvasRoot)
        {
            _canvasRoot = canvasRoot;
            MonoBehaviour.DontDestroyOnLoad(_canvasRoot.gameObject);
        }

        /// <summary>
        /// 加载并缓存view 资源
        /// </summary>
        /// <param name="loadPath"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        static GameObject LoadView(string loadPath)
        {
            if (!_viewDict.TryGetValue(loadPath, out var viewGo) || !viewGo)
            {
                viewGo = Resources.Load<GameObject>(loadPath);
                if (!viewGo)
                {
                    throw new IOException("There is no view prefab loaded by path： Resources/" + loadPath);
                }
                viewGo = GameObject.Instantiate(viewGo, CanvasRoot);
                viewGo.SetActive(false);
                _viewDict[loadPath] = viewGo;
            }

            return viewGo;
        }

        /// <summary>
        /// 预加载view资源
        /// </summary>
        public static void PreLoad<T>() where T : class, IContext
        {
            var context = Activator.CreateInstance<T>();
            LoadView(context.LoadPath);
        }

        static IContext LoadContext<T>() where T : class, IContext
        {
            var type = typeof(T);
            if (contextDict.TryGetValue(type, out var context))
            {
                return context;
            }
            else
            {
                context = Activator.CreateInstance<T>();
                var viewGo = LoadView(context.LoadPath);
                var view = viewGo.GetComponent<BaseView>();
                if (!view)
                {
                    throw new NullReferenceException("View component could not be found in " + context.LoadPath);
                }

                context.SetView(view);
                contextDict.Add(type, context);
            }

            return context;
        }

        static IContext GetCurrentContext()
        {
            if (container.Count > 0)
            {
                return container.Pop();
            }

            return null;
        }

        /// <summary>
        /// show context view
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void Push<T>() where T : class, IContext
        {
            var current = PeekOrNull();
            var next = LoadContext<T>();
            if (current == next && next != null)
            {
                Debug.LogWarning($"{next?.LoadPath} has open!");
            }
            else
            {
                if (current != null) container.Pop();
                current?.Pop();
                next.Push();
                container.Push(next);
            }

            Debug.Log("Context in stack:" + container.Count);
        }

        /// <summary>
        /// hide context view
        /// </summary>
        public static void Pop()
        {
            var current = GetCurrentContext();
            current?.Pop();
            var next = PeekOrNull();
            next?.Push();
            Debug.Log("Context in stack:" + container.Count);
        }

        #region show popup dialog windows

        static IContext PeekOrNull()
        {
            if (container.Count > 0)
            {
                return container.Peek();
            }

            return null;
        }

        public static void PushDialog<T>() where T : class, IContext
        {
            var current = PeekOrNull();
            current?.Pause();
            var dialog = LoadContext<T>();
            dialog.Push();
            container.Push(dialog);
        }

        public static void PopDialog<T>() where T : class, IContext
        {
            var dialog = LoadContext<T>();
            var current = PeekOrNull();
            if (dialog == current && dialog != null)
            {
                current.Pop();
                container.Pop();
                var next = PeekOrNull();
                next?.Resume();
            }
            else
            {
                Debug.LogError($"Current dialog is empty or not {dialog?.LoadPath}!");
            }
        }

        #endregion
    }

    public interface IContext
    {
        public string LoadPath { get; }
        public void Push();

        public void Pop();

        public void Pause();

        public void Resume();
        public void SetView(BaseView view);
    }

    public interface IContext<T> where T : BaseView
    {
        public T View { get; }
    }

    public interface IBelongToContext<T> where T : IContext
    {
        public T Context { get; }

        public void SetContext(T context);
    }
}