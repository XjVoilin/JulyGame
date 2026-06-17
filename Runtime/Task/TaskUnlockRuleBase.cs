using System;
using JulyArch;

namespace JulyGame.Task
{
    /// <summary>
    /// <see cref="ITaskUnlockRule"/> 的可选辅助基类。
    /// 持有 notifier 并提供 <see cref="RaiseChanged"/> 供子类在依赖状态变化后调用。
    /// 实现 <see cref="ICanEvent"/>，子类可直接 Subscribe 游戏事件驱动 <see cref="RaiseChanged"/>，
    /// 订阅随激活窗口自动管理：基座在任务处于 Locked 时激活、离开时休眠。
    /// </summary>
    public abstract class TaskUnlockRuleBase : ITaskUnlockRule, ICanEvent
    {
        private Action _onChanged;
        private bool _active;

        public IArchContext GetArchitecture() => GameArch.Context;

        public abstract bool CanUnlock();

        public void BindChangeNotifier(Action onChanged) => _onChanged = onChanged;

        /// <summary>子类在影响解锁判定的外部状态变化后调用，通知基座重新评估。</summary>
        protected void RaiseChanged() => _onChanged?.Invoke();

        /// <summary>进入活动窗口时调用。子类在此 Subscribe 影响解锁判定的游戏事件。</summary>
        protected virtual void OnActivate() { }

        /// <summary>离开活动窗口时调用。子类可在此做额外清理（事件订阅由基类自动清理）。</summary>
        protected virtual void OnDeactivate() { }

        internal void Activate()
        {
            if (_active) return;
            _active = true;
            OnActivate();
        }

        internal void Deactivate()
        {
            if (!_active) return;
            _active = false;
            OnDeactivate();
            this.UnsubscribeAll();
        }

        /// <summary>由基座在卸载任务时调用，集中清理子类订阅的游戏事件。</summary>
        internal void ClearSubscriptions() => Deactivate();
    }
}
