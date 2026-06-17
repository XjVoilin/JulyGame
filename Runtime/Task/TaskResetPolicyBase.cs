using System;
using JulyArch;

namespace JulyGame.Task
{
    /// <summary>
    /// <see cref="ITaskResetPolicy"/> 的可选辅助基类。
    /// 持有 notifier 并提供 <see cref="RaiseChanged"/> 供子类在检测到跨越重置边界后调用。
    /// 实现 <see cref="ICanEvent"/>，子类可直接 Subscribe 游戏事件驱动 <see cref="RaiseChanged"/>，
    /// 订阅在基座卸载任务时经 <see cref="ClearSubscriptions"/> 集中清理，避免泄漏。
    /// </summary>
    public abstract class TaskResetPolicyBase : ITaskResetPolicy, ICanEvent
    {
        private Action _onChanged;

        public IArchContext GetArchitecture() => GameArch.Context;

        public abstract DateTime GetNextResetUtc(DateTime utcNow);

        public void BindChangeNotifier(Action onChanged) => _onChanged = onChanged;

        /// <summary>子类在检测到跨越重置边界后调用，通知基座对所属任务执行重置。</summary>
        protected void RaiseChanged() => _onChanged?.Invoke();

        /// <summary>由基座在卸载任务时调用，集中清理子类订阅的游戏事件。</summary>
        internal void ClearSubscriptions() => this.UnsubscribeAll();
    }
}
