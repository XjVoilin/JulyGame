using System;
using System.Collections.Generic;
using System.Linq;
using JulyArch;
using JulyCore;
using JulyCore.Core;
using UnityEngine;

namespace JulyGame.Task
{
    public delegate bool RewardHandler(List<TaskReward> rewards);

    public delegate bool UnlockCheckHandler(TaskData taskData);

    [Serializable]
    public class TaskResetConfig
    {
        public float expireCheckIntervalSeconds = 60f;
    }

    public class TaskSystem : GameSystemBase, IUpdatableSystem, ITaskHandlerContext
    {
        public const string SaveKey = "task_progress";

        private TaskStore _store;
        private RewardHandler _rewardHandler;
        private UnlockCheckHandler _unlockCheckHandler;
        private ITaskResetScheduler _resetScheduler;
        private TaskResetConfig _resetConfig = new();

        private readonly Dictionary<TaskType, ITaskTypeHandler> _typeHandlers = new();
        private readonly ITimeCapability _timeCapability = new GfTimeCapabilityAdapter();

        private float _expireCheckAccumulator;

        protected override void OnInitialize()
        {
            _store = GetStore<TaskStore>();
        }

        protected override void OnStart()
        {
            _resetScheduler?.RegisterScheduledResets(_timeCapability, ResetTasksByType);
            TryUnlockAllTasks();
        }

        protected override void OnShutdown()
        {
            _resetScheduler?.UnregisterScheduledResets(_timeCapability);

            foreach (var handler in _typeHandlers.Values)
            {
                try { handler.Dispose(); }
                catch (Exception ex) { Debug.LogError($"[TaskSystem] Handler dispose failed: {ex.Message}"); }
            }

            _typeHandlers.Clear();
            _rewardHandler = null;
            _unlockCheckHandler = null;
            _resetScheduler = null;
        }

        public void OnUpdate(float deltaTime)
        {
            _expireCheckAccumulator += deltaTime;
            if (_expireCheckAccumulator < _resetConfig.expireCheckIntervalSeconds) return;

            _expireCheckAccumulator = 0f;
            CheckExpiredTasks();
        }

        public void SetResetConfig(TaskResetConfig config)
        {
            _resetConfig = config ?? new TaskResetConfig();
        }

        public void SetResetScheduler(ITaskResetScheduler scheduler) => _resetScheduler = scheduler;

        public void SetRewardHandler(RewardHandler handler) => _rewardHandler = handler;

        public void SetUnlockCheckHandler(UnlockCheckHandler handler) => _unlockCheckHandler = handler;

        public void RegisterTaskTypeHandler(ITaskTypeHandler handler)
        {
            if (handler == null)
            {
                Debug.LogWarning("[TaskSystem] RegisterTaskTypeHandler failed: handler is null");
                return;
            }

            var taskType = handler.TaskType;
            if (_typeHandlers.TryGetValue(taskType, out var oldHandler))
            {
                Debug.LogWarning($"[TaskSystem] Handler already exists and will be replaced: {taskType}");
                try { oldHandler.Dispose(); }
                catch (Exception ex) { Debug.LogError($"[TaskSystem] Old handler dispose failed: {ex.Message}"); }
            }

            try
            {
                handler.SetContext(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TaskSystem] Handler.SetContext failed: {ex.Message}");
                return;
            }

            _typeHandlers[taskType] = handler;

            try { handler.OnRegister(); }
            catch (Exception ex) { Debug.LogError($"[TaskSystem] Handler.OnRegister failed: {ex.Message}"); }
        }

        public void UnregisterTaskTypeHandler(TaskType taskType)
        {
            if (!_typeHandlers.TryGetValue(taskType, out var handler)) return;

            try { handler.Dispose(); }
            catch (Exception ex) { Debug.LogError($"[TaskSystem] Handler dispose failed: {ex.Message}"); }

            _typeHandlers.Remove(taskType);
        }

        public void RegisterTask(TaskData taskData)
        {
            _store.Store(taskData);
            MarkProgressDirty();
        }

        public void RegisterTasks(IEnumerable<TaskData> tasks)
        {
            _store.StoreBatch(tasks);
            MarkProgressDirty();
        }

        public void UnregisterTask(string taskId)
        {
            _store.Remove(taskId);
            MarkProgressDirty();
        }

        public void ClearAllTasks()
        {
            _store.Clear();
            MarkProgressDirty();
        }

        public TaskData GetTask(string taskId) => _store.Get(taskId);

        public IReadOnlyList<TaskData> GetAllTasks() => _store.GetAll();

        public IReadOnlyList<TaskData> GetTasksByType(TaskType type) => _store.GetByType(type);

        public IReadOnlyList<TaskData> GetTasksByState(TaskState state) => _store.GetByState(state);

        public IReadOnlyList<TaskData> GetTasksByGroup(string group) => _store.GetByGroup(group);

        public bool CanUnlockTask(string taskId)
        {
            var task = _store.Get(taskId);
            if (task == null) return false;
            if (task.State != TaskState.Locked) return false;
            if (IsTaskExpired(task)) return false;
            if (!ArePrerequisiteTasksCompleted(task)) return false;
            if (_unlockCheckHandler != null && !_unlockCheckHandler(task)) return false;
            return true;
        }

        public bool UnlockTask(string taskId)
        {
            if (!CanUnlockTask(taskId))
            {
                Debug.LogWarning($"[TaskSystem] Unlock failed: {taskId}");
                return false;
            }

            var task = _store.Get(taskId);
            var oldState = task.State;
            task.State = TaskState.InProgress;
            _store.Update(task);
            MarkProgressDirty();

            ActivateHandler(task);

            Publish(new TaskUnlockedEvent { TaskId = taskId, TaskData = task });
            Publish(new TaskStateChangedEvent
            {
                TaskId = taskId,
                OldState = oldState,
                NewState = TaskState.InProgress,
                TaskData = task
            });

            return true;
        }

        public int TryUnlockAllTasks()
        {
            var count = 0;
            foreach (var task in _store.GetByState(TaskState.Locked))
            {
                if (CanUnlockTask(task.TaskId) && UnlockTask(task.TaskId))
                    count++;
            }

            return count;
        }

        public void UpdateProgress(TaskConditionType conditionType, string param, int delta = 1)
        {
            foreach (var (taskId, conditionId) in _store.QueryByCondition(conditionType, param))
            {
                var task = _store.Get(taskId);
                if (task == null || task.State != TaskState.InProgress) continue;

                UpdateTaskCondition(task, conditionId, delta, false);
            }
        }

        public void UpdateReachProgress(string param, int value)
        {
            foreach (var (taskId, conditionId) in _store.QueryByCondition(TaskConditionType.Reach, param))
            {
                var task = _store.Get(taskId);
                if (task == null || task.State != TaskState.InProgress) continue;

                var condition = task.Conditions?.Find(c => c.ConditionId == conditionId);
                if (condition == null) continue;

                var newValue = Math.Min(Math.Max(condition.CurrentValue, value), condition.TargetValue);
                UpdateTaskCondition(task, conditionId, newValue, true);
            }
        }

        public void UpdateTaskProgress(string taskId, string conditionId, int value)
        {
            var task = _store.Get(taskId);
            if (task == null) return;

            UpdateTaskCondition(task, conditionId, value, true);
        }

        public bool ClaimReward(string taskId)
        {
            var task = _store.Get(taskId);
            if (task == null)
            {
                Debug.LogWarning($"[TaskSystem] ClaimReward failed: task not found {taskId}");
                return false;
            }

            if (task.State != TaskState.Completed)
            {
                Debug.LogWarning($"[TaskSystem] ClaimReward failed: task {taskId} is not Completed");
                return false;
            }

            if (task.Rewards != null && task.Rewards.Count > 0)
            {
                if (_rewardHandler == null)
                {
                    Debug.LogWarning(
                        $"[TaskSystem] Task {taskId} has {task.Rewards.Count} rewards but no RewardHandler is set.");
                }
                else if (!_rewardHandler(task.Rewards))
                {
                    Debug.LogWarning($"[TaskSystem] Reward dispatch failed: {taskId}");
                    return false;
                }
            }

            var oldState = task.State;
            task.State = TaskState.Rewarded;
            _store.Update(task);
            MarkProgressDirty();

            Publish(new TaskStateChangedEvent
            {
                TaskId = taskId,
                OldState = oldState,
                NewState = TaskState.Rewarded,
                TaskData = task
            });

            Publish(new TaskRewardClaimedEvent
            {
                TaskId = taskId,
                Rewards = task.Rewards,
                TaskData = task
            });

            TryUnlockAllTasks();
            return true;
        }

        public List<string> ClaimAllRewards()
        {
            var claimed = new List<string>();
            foreach (var task in _store.GetByState(TaskState.Completed))
            {
                if (ClaimReward(task.TaskId))
                    claimed.Add(task.TaskId);
            }

            return claimed;
        }

        public bool ResetTask(string taskId)
        {
            var task = _store.Get(taskId);
            if (task == null) return false;

            if (task.Conditions != null)
            {
                foreach (var condition in task.Conditions)
                    condition.CurrentValue = 0;
            }

            task.State = TaskState.InProgress;
            _store.Update(task);
            MarkProgressDirty();
            return true;
        }

        public void ResetTasksByType(TaskType type)
        {
            foreach (var task in _store.GetByType(type))
            {
                if (task.State == TaskState.Rewarded || task.State == TaskState.InProgress)
                    ResetTask(task.TaskId);
            }
        }

        public Dictionary<string, TaskSaveData> ExportProgress()
        {
            return _store.ExportProgress();
        }

        public void ImportProgress(Dictionary<string, TaskSaveData> data)
        {
            _store.ImportProgress(data);
            MarkProgressDirty();
        }

        public void LoadFromConfig(TaskConfig config, DateTime? baseTime = null)
        {
            if (config == null) return;
            RegisterTask(config.ToTaskData(baseTime));
        }

        public void LoadFromConfigTable(TaskConfigTable configTable, DateTime? baseTime = null)
        {
            if (configTable?.Tasks == null) return;
            RegisterTasks(configTable.ToTaskDataList(baseTime));
        }

        JulyEvents.IEventBus ITaskHandlerContext.EventBus => GetArchitecture().Event;

        void ITaskHandlerContext.UpdateProgress(TaskConditionType conditionType, string param, int delta)
        {
            UpdateProgress(conditionType, param, delta);
        }

        void ITaskHandlerContext.UpdateTaskProgress(string taskId, string conditionId, int value)
        {
            UpdateTaskProgress(taskId, conditionId, value);
        }

        TaskData ITaskHandlerContext.GetTask(string taskId) => GetTask(taskId);

        private void UpdateTaskCondition(TaskData task, string conditionId, int valueOrDelta, bool isAbsolute)
        {
            var condition = task.Conditions?.Find(c => c.ConditionId == conditionId);
            if (condition == null) return;

            var oldValue = condition.CurrentValue;
            var wasCompleted = condition.IsCompleted;
            var wasTaskCompleted = task.AreAllConditionsCompleted();

            if (isAbsolute)
            {
                condition.CurrentValue = Math.Min(
                    Math.Max(condition.CurrentValue, valueOrDelta),
                    condition.TargetValue);
            }
            else
            {
                condition.CurrentValue = Math.Min(condition.CurrentValue + valueOrDelta, condition.TargetValue);
            }

            _store.Update(task);
            MarkProgressDirty();

            var justCompleted = !wasCompleted && condition.IsCompleted;
            var taskJustCompleted = !wasTaskCompleted && task.AreAllConditionsCompleted();

            Publish(new TaskProgressUpdatedEvent
            {
                TaskId = task.TaskId,
                ConditionId = conditionId,
                OldValue = oldValue,
                NewValue = condition.CurrentValue,
                TargetValue = condition.TargetValue,
                ConditionJustCompleted = justCompleted,
                TaskJustCompleted = taskJustCompleted
            });

            if (taskJustCompleted && task.State == TaskState.InProgress)
            {
                var oldState = task.State;
                task.State = TaskState.Completed;
                _store.Update(task);
                MarkProgressDirty();

                Publish(new TaskStateChangedEvent
                {
                    TaskId = task.TaskId,
                    OldState = oldState,
                    NewState = TaskState.Completed,
                    TaskData = task
                });

                Publish(new TaskCompletedEvent { TaskId = task.TaskId, TaskData = task });
                OnTaskCompleted(task.TaskId);
            }
        }

        private void OnTaskCompleted(string taskId)
        {
            foreach (var nextTask in _store.Query(t =>
                         t.PrerequisiteTaskIds != null &&
                         t.PrerequisiteTaskIds.Contains(taskId) &&
                         t.State == TaskState.Locked))
            {
                if (CanUnlockTask(nextTask.TaskId))
                    UnlockTask(nextTask.TaskId);
            }

            TryUnlockAllTasks();
        }

        private bool ArePrerequisiteTasksCompleted(TaskData task)
        {
            if (task.PrerequisiteTaskIds == null || task.PrerequisiteTaskIds.Count == 0)
                return true;

            foreach (var prereqId in task.PrerequisiteTaskIds)
            {
                var prereqTask = _store.Get(prereqId);
                if (prereqTask == null || prereqTask.State < TaskState.Completed)
                    return false;
            }

            return true;
        }

        private bool IsTaskExpired(TaskData task)
        {
            return task.ExpireTime.HasValue && GF.Time.ServerTimeUtc > task.ExpireTime.Value;
        }

        private void CheckExpiredTasks()
        {
            var now = GF.Time.ServerTimeUtc;

            foreach (var task in _store.GetByState(TaskState.InProgress))
            {
                if (!task.ExpireTime.HasValue || now <= task.ExpireTime.Value) continue;

                var oldState = task.State;
                task.State = TaskState.Expired;
                _store.Update(task);
                MarkProgressDirty();

                Publish(new TaskStateChangedEvent
                {
                    TaskId = task.TaskId,
                    OldState = oldState,
                    NewState = TaskState.Expired,
                    TaskData = task
                });
            }
        }

        private void ActivateHandler(TaskData taskData)
        {
            if (!_typeHandlers.TryGetValue(taskData.Type, out var handler)) return;

            try { handler.OnTaskUnlocked(taskData); }
            catch (Exception ex) { Debug.LogError($"[TaskSystem] Handler.OnTaskUnlocked failed: {ex.Message}"); }
        }

        private void MarkProgressDirty()
        {
            if (GF.Save.IsRegistered(SaveKey))
                GF.Save.MarkDirty(SaveKey);
        }

        private sealed class GfTimeCapabilityAdapter : ITimeCapability
        {
            public DateTime ServerTimeUtc => GF.Time.ServerTimeUtc;
            public long ServerTimeSeconds => GF.Time.ServerTimeUtcTimestamp;

            public int ScheduleOnce(float delay, Action callback, bool useRealTime = false)
                => GF.Time.ScheduleOnce(delay, callback, useRealTime);

            public int ScheduleRepeat(float interval, Action callback, bool useRealTime = false, int repeatCount = -1)
                => GF.Time.ScheduleRepeat(interval, callback, useRealTime, repeatCount);

            public bool CancelTimer(int timerId) => GF.Time.CancelTimer(timerId);
        }
    }
}
