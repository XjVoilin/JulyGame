using System;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务条件扩展点。由接入方实现，封装"某项进度是否达成"的全部业务判定逻辑。
    /// 基座在注册任务时通过 <see cref="BindChangeNotifier"/> 注入回调，条件内部状态变化后
    /// 由接入方主动调用该回调通知基座重新评估，基座不做轮询。
    /// </summary>
    /// <remarks>
    /// 契约（接入方实现时必须遵守）：
    /// <list type="bullet">
    /// <item><see cref="ConditionId"/> 在同一任务内必须唯一且稳定，基座以 (TaskId, ConditionId) 为键缓存进度。</item>
    /// <item><see cref="IsCompleted"/> 是"条件是否达成"的唯一权威。基座以它判定条件完成与任务完成，
    /// 不依赖 <see cref="Progress"/> 是否等于 1。</item>
    /// <item><see cref="Progress"/> 仅用于 UI 展示与变更通知，取值应规整到 [0,1]。
    /// 对没有连续进度的布尔型条件，未完成返回 0、完成返回 1 即可。</item>
    /// <item><see cref="Reset"/> 必须把内部计数清零，使条件回到"未达成"初始态。
    /// 基座在任务重置（手动或重置策略跨越边界）与解锁时调用它。</item>
    /// <item><see cref="BindChangeNotifier"/> 由基座在注册时调用一次，传入一个通知回调。
    /// 条件内部计数/状态发生变化后必须调用该回调，以触发基座对所属任务的即时评估。
    /// 基座会在静默期（如 Reset）内忽略回调，接入方无需操心去重。</item>
    /// </list>
    /// </remarks>
    public interface ITaskCondition
    {
        /// <summary>同一任务内唯一且稳定的条件标识。</summary>
        int ConditionId { get; }

        /// <summary>条件是否已达成。基座据此判定条件完成与任务整体完成（权威字段）。</summary>
        bool IsCompleted { get; }

        /// <summary>展示用进度，规整到 [0,1]。仅用于 UI 与进度变更事件，不参与完成判定。</summary>
        float Progress { get; }

        /// <summary>
        /// 清零内部计数，使条件回到未达成初始态。
        /// 由基座在任务重置或解锁时调用；接入方不应在此触发任何业务副作用（如发奖、UI）。
        /// </summary>
        void Reset();

        /// <summary>
        /// 由基座在注册任务时调用，注入变更通知回调。
        /// 条件内部计数/状态发生变化后，接入方应调用 <paramref name="onChanged"/> 通知基座。
        /// </summary>
        void BindChangeNotifier(Action onChanged);
    }
}
