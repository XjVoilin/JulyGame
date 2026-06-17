namespace JulyGame.Task
{
    /// <summary>任务注册到系统时发布。</summary>
    public struct TaskRegisteredEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    /// <summary>任务从 Locked 进入 InProgress（解锁规则全部满足或手动解锁）时发布。</summary>
    public struct TaskUnlockedEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    /// <summary>某个条件进度发生变化时发布，供 UI 刷新。</summary>
    public struct TaskProgressUpdatedEvent
    {
        public int TaskId;
        public int ConditionId;
        public float OldProgress;
        public float NewProgress;
        /// <summary>本次变化是否使该条件从未达成变为达成。</summary>
        public bool ConditionJustCompleted;
    }

    /// <summary>某个条件首次达成时发布。</summary>
    public struct TaskConditionCompletedEvent
    {
        public int TaskId;
        public int ConditionId;
    }

    /// <summary>任务全部条件达成、进入 Completed 时发布。接入方通常在此发奖。</summary>
    public struct TaskCompletedEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    /// <summary>任务状态发生任意流转时发布（解锁、完成、重置都会附带触发）。</summary>
    public struct TaskStateChangedEvent
    {
        public int TaskId;
        public ETaskState OldState;
        public ETaskState NewState;
        public TaskData TaskData;
    }

    /// <summary>任务被重置（手动或重置策略跨越边界）回到 InProgress 时发布。</summary>
    public struct TaskResetEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    /// <summary>任务从系统中移除时发布。</summary>
    public struct TaskRemovedEvent
    {
        public int TaskId;
    }
}
