using System;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务解锁规则扩展点。由接入方实现，封装"任务能否从 Locked 进入 InProgress"的前置条件
    /// （如前置任务完成、玩家等级达标、活动开启等）。
    /// </summary>
    /// <remarks>
    /// 契约：
    /// <list type="bullet">
    /// <item>一个任务可挂多条规则，基座按"全部满足才解锁"（逻辑与）处理。</item>
    /// <item><see cref="CanUnlock"/> 应为无副作用的纯查询。</item>
    /// <item>抛出异常将被基座捕获并视为"未满足"，对应任务保持 Locked。</item>
    /// <item><see cref="BindChangeNotifier"/> 由基座在注册时调用一次，传入通知回调。
    /// 当影响解锁判定的外部状态发生变化时（如前置任务完成、玩家升级），
    /// 接入方应调用该回调通知基座重新评估解锁。基座对依赖内容一无所知，不泄漏业务细节。</item>
    /// </list>
    /// </remarks>
    public interface ITaskUnlockRule
    {
        /// <summary>当前是否满足解锁前置条件。应为无副作用的纯查询。</summary>
        bool CanUnlock();

        /// <summary>
        /// 由基座在注册任务时调用，注入变更通知回调。
        /// 影响解锁判定的外部状态变化后，接入方应调用 <paramref name="onChanged"/> 通知基座重新评估。
        /// </summary>
        void BindChangeNotifier(Action onChanged);
    }
}
