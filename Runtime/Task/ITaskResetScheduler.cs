using System;
using JulyCore.Core;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务重置调度器接口。
    /// 由业务层实现，定义哪些任务类型需要定时重置、何时重置。
    /// 框架层不预设任何业务特定的重置逻辑（如每日/每周）。
    /// </summary>
    public interface ITaskResetScheduler
    {
        /// <summary>
        /// 注册所有重置调度（在 TaskSystem 初始化时调用）。
        /// 实现方应使用 ITimeCapability 注册定时器，并在回调中调用 resetAction 触发重置。
        /// </summary>
        void RegisterScheduledResets(ITimeCapability timeCapability, Action<TaskType> resetAction);

        /// <summary>
        /// 注销所有重置调度（在 TaskSystem 关闭时调用）
        /// </summary>
        void UnregisterScheduledResets(ITimeCapability timeCapability);
    }
}
