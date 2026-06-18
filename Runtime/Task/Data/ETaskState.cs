namespace JulyGame.Task
{
    /// <summary>
    /// 任务状态机。流转方向：Locked → InProgress → Completed，
    /// 重置可使 Completed/InProgress 回到 InProgress。枚举顺序用于比较，不可随意调整。
    /// </summary>
    public enum ETaskState
    {
        /// <summary>未解锁。等待 <see cref="ITaskUnlockRule"/> 全部满足。</summary>
        Locked,

        /// <summary>进行中。条件变化时 push 通知基座评估，全部达成则流转到 Completed。</summary>
        InProgress,

        /// <summary>已完成。可由重置策略或手动重置回到 InProgress。</summary>
        Completed
    }
}
