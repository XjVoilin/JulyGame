using System;
using System.Collections.Generic;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务条件配置（配置表数据）
    /// </summary>
    [Serializable]
    public class TaskConditionConfig
    {
        public string ConditionId { get; set; }
        public TaskConditionType Type { get; set; }
        public string Param { get; set; }
        public int TargetValue { get; set; }

        public TaskCondition ToCondition()
        {
            return new TaskCondition
            {
                ConditionId = ConditionId,
                Type = Type,
                Param = Param,
                TargetValue = TargetValue,
                CurrentValue = 0
            };
        }
    }

    /// <summary>
    /// 任务奖励配置（配置表数据）
    /// </summary>
    [Serializable]
    public class TaskRewardConfig
    {
        public string RewardType { get; set; }
        public string Param { get; set; }
        public int Amount { get; set; }

        public TaskReward ToReward()
        {
            return new TaskReward
            {
                RewardType = RewardType,
                Param = Param,
                Amount = Amount
            };
        }
    }

    /// <summary>
    /// 任务配置（配置表数据）
    /// </summary>
    [Serializable]
    public class TaskConfig
    {
        public string TaskId { get; set; }
        public TaskType Type { get; set; }
        public string Group { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskState InitialState { get; set; } = TaskState.Locked;
        public List<TaskConditionConfig> Conditions { get; set; }
        public List<TaskRewardConfig> Rewards { get; set; }
        public List<string> PrerequisiteTaskIds { get; set; }
        public Dictionary<string, object> UnlockParams { get; set; }
        public int Priority { get; set; }
        public int DurationSeconds { get; set; }
        public Dictionary<string, object> ExtraData { get; set; }

        public TaskData ToTaskData(DateTime? baseTime = null)
        {
            var taskData = new TaskData
            {
                TaskId = TaskId,
                Type = Type,
                Group = Group,
                Name = Name,
                Description = Description,
                State = InitialState,
                Priority = Priority,
                PrerequisiteTaskIds = PrerequisiteTaskIds != null
                    ? new List<string>(PrerequisiteTaskIds)
                    : new List<string>(),
                UnlockParams = UnlockParams != null
                    ? new Dictionary<string, object>(UnlockParams)
                    : new Dictionary<string, object>(),
                ExtraData = ExtraData != null
                    ? new Dictionary<string, object>(ExtraData)
                    : new Dictionary<string, object>()
            };

            if (Conditions != null)
            {
                taskData.Conditions = new List<TaskCondition>();
                foreach (var config in Conditions)
                {
                    taskData.Conditions.Add(config.ToCondition());
                }
            }

            if (Rewards != null)
            {
                taskData.Rewards = new List<TaskReward>();
                foreach (var config in Rewards)
                {
                    taskData.Rewards.Add(config.ToReward());
                }
            }

            if (DurationSeconds > 0)
            {
                var startTime = baseTime ?? DateTime.UtcNow;
                taskData.ExpireTime = startTime.AddSeconds(DurationSeconds);
            }

            return taskData;
        }
    }

    /// <summary>
    /// 任务配置表
    /// </summary>
    [Serializable]
    public class TaskConfigTable
    {
        public List<TaskConfig> Tasks { get; set; } = new List<TaskConfig>();

        public List<TaskData> ToTaskDataList(DateTime? baseTime = null)
        {
            var result = new List<TaskData>();
            if (Tasks != null)
            {
                foreach (var config in Tasks)
                {
                    result.Add(config.ToTaskData(baseTime));
                }
            }
            return result;
        }
    }
}

