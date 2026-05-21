using System.Collections.Generic;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务状态变更事件
    /// </summary>
    public struct TaskStateChangedEvent
    {
        public string TaskId;
        public TaskState OldState;
        public TaskState NewState;
        public TaskData TaskData;
    }

    /// <summary>
    /// 任务进度更新事件
    /// </summary>
    public struct TaskProgressUpdatedEvent
    {
        public string TaskId;
        public string ConditionId;
        public int OldValue;
        public int NewValue;
        public int TargetValue;
        public bool ConditionJustCompleted;
        public bool TaskJustCompleted;
    }

    /// <summary>
    /// 任务完成事件
    /// </summary>
    public struct TaskCompletedEvent
    {
        public string TaskId;
        public TaskData TaskData;
    }

    /// <summary>
    /// 任务奖励领取事件
    /// </summary>
    public struct TaskRewardClaimedEvent
    {
        public string TaskId;
        public List<TaskReward> Rewards;
        public TaskData TaskData;
    }

    /// <summary>
    /// 任务解锁事件
    /// </summary>
    public struct TaskUnlockedEvent
    {
        public string TaskId;
        public TaskData TaskData;
    }
}
