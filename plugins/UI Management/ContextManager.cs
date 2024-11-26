using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gary.UIManagement
{
    public class ContextManager
    {
        public static readonly Stack<IContext> container = new Stack<IContext>();
        public static readonly Dictionary<Type, IContext> resourcesDict = new Dictionary<Type, IContext>();
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
        }

        static IContext Load<T>() where T : class, IContext
        {
            var type = typeof(T);
            if (resourcesDict.TryGetValue(type, out var context))
            {
                return context;
            }
            else
            {
                context = Activator.CreateInstance<T>();
                var prefab = Resources.Load<GameObject>(context.LoadPath);
                if (!prefab)
                {
                    throw new IOException("There is no view loaded by path Resources/" + context.LoadPath);
                }
                else
                {
                    var viewGo = GameObject.Instantiate(prefab, CanvasRoot);
                    var view = viewGo.GetComponent<BaseView>();
                    if (!view)
                    {
                        throw new NullReferenceException("View component could not be found in " + context.LoadPath);
                    }
                    else
                    {
                        context.SetView(view);
                    }
                }

                resourcesDict.Add(type, context);
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
        public static void Push<T>() where T :class, IContext
        {
            var current = PeekOrNull();
            var next = Load<T>();
            if (current == next && next != null)
            {
                Debug.LogWarning($"{next?.LoadPath} has open!");
            }
            else
            {
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
            var dialog = Load<T>();
            ((IContext)dialog).Push();
            container.Push(dialog);
        }

        public static void PopDialog<T>() where T : class, IContext
        {
            var dialog = Load<T>();
            var current = PeekOrNull();
            if (dialog == current && dialog != null)
            {
                container.Pop()?.Pop();
                var next = GetCurrentContext();
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