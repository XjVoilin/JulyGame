using System;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务重置策略扩展点。由接入方实现，封装"下一次重置发生在何时"的时间规则
    /// （如每日 0 点、每周一、活动结束等）。
    /// </summary>
    /// <remarks>
    /// 契约：
    /// <list type="bullet">
    /// <item>返回值为 UTC 时间。基座统一以 UTC 存储与比较重置边界，避免时区/夏令时问题。</item>
    /// <item>同一时间点多次调用应返回相同结果（幂等）；跨越边界后应返回新的、更晚的边界。</item>
    /// <item>基座以"<see cref="GetNextResetUtc"/> 的结果 Ticks 发生变化"作为重置信号，
    /// 因此实现需保证边界推进是单调的。</item>
    /// <item>重置由接入方调用 <see cref="TaskSystemBase.SweepResets"/> 在合适时机
    /// （前台恢复、App 心跳等）批量触发检查，策略本身只需提供边界、无需主动通知。</item>
    /// </list>
    /// </remarks>
    public interface ITaskResetPolicy
    {
        /// <summary>给定当前 UTC 时间，返回下一次重置的 UTC 时间点。</summary>
        DateTime GetNextResetUtc(DateTime utcNow);
    }
}
