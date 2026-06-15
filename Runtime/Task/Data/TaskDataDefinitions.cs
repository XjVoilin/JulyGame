using System;
using System.Collections.Generic;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务类型（使用 int 而非 enum，由业务层自定义具体含义）。
    /// 框架层不预设任何业务枚举值，避免抽象泄漏。
    /// 
    /// 业务层使用示例：
    /// <code>
    /// public static class MyTaskType
    /// {
    ///     public const int Main = 0;
    ///     public const int Side = 1;
    ///     public const int Daily = 2;
    ///     public const int Weekly = 3;
    ///     public const int Achievement = 4;
    /// }
    /// </code>
    /// </summary>
    public readonly struct TaskType : IEquatable<TaskType>
    {
        public readonly int Value;

        public TaskType(int value) => Value = value;

        public static implicit operator TaskType(int value) => new TaskType(value);
        public static implicit operator int(TaskType type) => type.Value;

        public bool Equals(TaskType other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TaskType other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(TaskType left, TaskType right) => left.Value == right.Value;
        public static bool operator !=(TaskType left, TaskType right) => left.Value != right.Value;
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum TaskState
    {
        /// <summary>
        /// 未解锁
        /// </summary>
        Locked,

        /// <summary>
        /// 进行中
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成（待领奖）
        /// </summary>
        Completed,

        /// <summary>
        /// 已领取奖励
        /// </summary>
        Rewarded,

        /// <summary>
        /// 已过期
        /// </summary>
        Expired
    }

    /// <summary>
    /// 任务排序方式
    /// </summary>
    public enum TaskSortBy
    {
        /// <summary>
        /// 按优先级排序
        /// </summary>
        Priority,
        
        /// <summary>
        /// 按类型排序
        /// </summary>
        Type,
        
        /// <summary>
        /// 按状态排序
        /// </summary>
        State
    }

    /// <summary>
    /// 任务条件类型
    /// </summary>
    public enum TaskConditionType
    {
        /// <summary>
        /// 累计计数（增量更新）
        /// 每次事件触发时累加数值。Param 为业务层定义的 int 常量或枚举值。
        /// </summary>
        Accumulate,

        /// <summary>
        /// 达到数值（绝对值更新）
        /// 直接检查当前数值是否达到目标。Param 为业务层定义的 int 常量或枚举值。
        /// 注意：使用此类型时，应调用 UpdateReachProgress 或 UpdateTaskProgress 设置绝对值。
        /// </summary>
        Reach
    }

    /// <summary>
    /// 任务条件定义（业务数据）
    /// </summary>
    [Serializable]
    public class TaskCondition
    {
        /// <summary>
        /// 条件ID
        /// </summary>
        public string ConditionId { get; set; }

        /// <summary>
        /// 条件类型
        /// </summary>
        public TaskConditionType Type { get; set; }

        /// <summary>
        /// 条件参数（由业务层定义的 int 常量或枚举值）
        /// </summary>
        public int Param { get; set; }

        /// <summary>
        /// 目标数值
        /// </summary>
        public int TargetValue { get; set; }

        /// <summary>
        /// 当前数值
        /// </summary>
        public int CurrentValue { get; set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => CurrentValue >= TargetValue;

        /// <summary>
        /// 完成进度（0-1）
        /// </summary>
        public float Progress => TargetValue > 0 ? Math.Min(1f, (float)CurrentValue / TargetValue) : 0f;

        /// <summary>
        /// 克隆条件
        /// </summary>
        public TaskCondition Clone()
        {
            return new TaskCondition
            {
                ConditionId = ConditionId,
                Type = Type,
                Param = Param,
                TargetValue = TargetValue,
                CurrentValue = CurrentValue
            };
        }
    }

    /// <summary>
    /// 任务奖励定义（业务数据）
    /// </summary>
    [Serializable]
    public class TaskReward
    {
        /// <summary>
        /// 奖励类型（由业务层定义，如：Gold, Diamond, Item等）
        /// </summary>
        public string RewardType { get; set; }

        /// <summary>
        /// 奖励参数（如：道具ID）
        /// </summary>
        public string Param { get; set; }

        /// <summary>
        /// 奖励数量
        /// </summary>
        public int Amount { get; set; }
    }

    /// <summary>
    /// 任务数据（运行时业务数据）
    /// </summary>
    [Serializable]
    public class TaskData
    {
        /// <summary>
        /// 任务ID（唯一标识）
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// 任务类型
        /// </summary>
        public TaskType Type { get; set; }

        /// <summary>
        /// 任务分组（用于UI分类显示）
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 任务描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        public TaskState State { get; set; } = TaskState.Locked;

        /// <summary>
        /// 任务条件列表
        /// </summary>
        public List<TaskCondition> Conditions { get; set; } = new List<TaskCondition>();

        /// <summary>
        /// 任务奖励列表
        /// </summary>
        public List<TaskReward> Rewards { get; set; } = new List<TaskReward>();

        /// <summary>
        /// 前置任务ID列表
        /// </summary>
        public List<string> PrerequisiteTaskIds { get; set; } = new List<string>();

        /// <summary>
        /// 解锁条件参数（如：等级要求）
        /// </summary>
        public Dictionary<string, object> UnlockParams { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 任务优先级（用于排序）
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 过期时间（UTC，null表示永不过期）
        /// </summary>
        public DateTime? ExpireTime { get; set; }

        /// <summary>
        /// 自定义扩展数据
        /// </summary>
        public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 是否所有条件都已完成
        /// </summary>
        public bool AreAllConditionsCompleted()
        {
            if (Conditions == null || Conditions.Count == 0)
                return true;

            foreach (var condition in Conditions)
            {
                if (!condition.IsCompleted)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取总体完成进度
        /// </summary>
        public float GetOverallProgress()
        {
            if (Conditions == null || Conditions.Count == 0)
                return State >= TaskState.Completed ? 1f : 0f;

            float totalProgress = 0f;
            foreach (var condition in Conditions)
            {
                totalProgress += condition.Progress;
            }
            return totalProgress / Conditions.Count;
        }
    }

    /// <summary>
    /// 任务存档数据
    /// </summary>
    [Serializable]
    public class TaskSaveData
    {
        /// <summary>
        /// 任务状态
        /// </summary>
        public TaskState State { get; set; }

        /// <summary>
        /// 条件进度（条件ID -> 当前值）
        /// </summary>
        public Dictionary<string, int> ConditionProgress { get; set; } = new Dictionary<string, int>();
    }
}

