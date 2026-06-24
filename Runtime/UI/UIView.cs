using System.Threading;
using Cysharp.Threading.Tasks;
using JulyArch;
using UnityEngine;

namespace JulyGame
{
    /// <summary>
    /// UI 面板基类 — 继承 GameView，由 UISystem 驱动生命周期。
    /// </summary>
    public abstract class UIView : GameView
    {
        private object _data;

        public bool IsOpened { get; private set; }
        public int WindowId { get; internal set; }

        #region Internal — UISystem 驱动

        internal void InternalSetData(object data) => _data = data;

        internal void InternalBeforeOpen()
        {
            OnBeforeOpen();
        }

        internal void InternalOpen()
        {
            IsOpened = true;
            OnOpen();
        }

        internal void InternalClose()
        {
            if (!IsOpened) return;
            IsOpened = false;
            OnClose();
        }

        internal void InternalAfterClose()
        {
            OnAfterClose();
        }

        #endregion

        #region GameView 钩子封堵 — UIView 子类应使用 OnBeforeOpen/OnOpen/OnClose/OnAfterClose

        protected sealed override void OnViewEnable() { }
        protected sealed override void OnViewDisable() { }

        #endregion

        #region 子类 UI 生命周期钩子

        /// <summary>数据已注入，设置 UI 初始状态。在 open 动画之前调用。</summary>
        protected virtual void OnBeforeOpen() { }

        /// <summary>Open 动画结束，面板可交互。订阅事件推荐放这里。</summary>
        protected virtual void OnOpen() { }

        /// <summary>关闭开始，业务清理。</summary>
        protected virtual void OnClose() { }

        /// <summary>Close 动画结束，即将销毁。</summary>
        protected virtual void OnAfterClose() { }

        #endregion

        #region 工具方法

        protected T GetData<T>()
        {
            if (_data is T d) return d;
            return default;
        }

        protected void CloseWindow(bool destroy = true, UIAnimationType? animationType = null)
        {
            if (!IsOpened) return;
            GetSystem<UISystem>().Close(this, destroy, animationType);
        }

        protected UniTask CloseWindowAsync(bool destroy = true, UIAnimationType? animationType = null,
            CancellationToken ct = default)
        {
            if (!IsOpened) return UniTask.CompletedTask;
            return GetSystem<UISystem>().CloseAsync(this, destroy, animationType, ct);
        }

        #endregion
    }
}
