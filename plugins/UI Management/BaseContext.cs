using System;
using System.IO;
using UnityEngine;

namespace Gary.UIManagement
{
    public abstract class BaseContext<T> : IContext<T>,IContext where T : BaseView
    {
        private T _view;
        public T View => _view;

        public abstract string LoadPath { get; }
        
        void IContext.SetView(BaseView view)
        {
            _view = view as T;
            OnSetView(_view);
        }

        /// <summary>
        /// call after load and set view
        /// </summary>
        /// <param name="view"></param>
        protected abstract void OnSetView(T view);

        /// <summary>
        /// 显示
        /// </summary>
        void IContext.Push()
        {
            OnPush();
        }

        /// <summary>
        /// 显示之前的回调
        /// </summary>
        protected abstract void OnPush();

        /// <summary>
        /// 弹出，关闭显示
        /// </summary>
        void IContext.Pop()
        {
            OnPop();
        }
        
        void IContext.Resume()
        {
            OnResume();
        }
        
        /// <summary>
        /// 加载单独窗口时触发当前context的暂停
        /// </summary>
        void IContext.Pause()
        {
            OnPause();
        }
        
        /// <summary>
        /// 弹出,之前的回调
        /// </summary>
        protected abstract void OnPop();

        /// <summary>
        /// 加载模拟窗口时触发当前context的暂停
        /// </summary>
        protected abstract void OnPause();

        /// <summary>
        /// 加载模拟窗口时触发当前context的暂停
        /// </summary>
        protected abstract void OnResume();

        protected void ToggleViewObject(bool isOn)
        {
            this.View.gameObject.SetActive(isOn);
        }
    }
}