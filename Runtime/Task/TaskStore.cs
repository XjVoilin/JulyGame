using System;
using System.Collections.Generic;
using System.Linq;
using JulyArch;

namespace JulyGame.Task
{
    public class TaskStoreData
    {
        public Dictionary<string, TaskData> Tasks = new();
        public Dictionary<(TaskConditionType, int), List<(string taskId, string conditionId)>> ConditionIndex = new();
    }

    public class TaskStore : StoreBase<TaskStoreData>
    {
        public TaskData Get(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return null;
            Data.Tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public IReadOnlyList<TaskData> GetAll()
        {
            return Data.Tasks.Values.ToList();
        }

        public IReadOnlyList<TaskData> GetByType(TaskType type)
        {
            return Data.Tasks.Values.Where(t => t.Type == type).ToList();
        }

        public IReadOnlyList<TaskData> GetByState(TaskState state)
        {
            return Data.Tasks.Values.Where(t => t.State == state).ToList();
        }

        public IReadOnlyList<TaskData> GetByGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return Array.Empty<TaskData>();
            return Data.Tasks.Values.Where(t => t.Group == group).ToList();
        }

        public IReadOnlyList<TaskData> Query(Func<TaskData, bool> predicate)
        {
            return Data.Tasks.Values.Where(predicate).ToList();
        }

        public IReadOnlyList<(string taskId, string conditionId)> QueryByCondition(
            TaskConditionType conditionType, int param)
        {
            if (!Data.ConditionIndex.TryGetValue((conditionType, param), out var matches))
                return Array.Empty<(string, string)>();
            return matches;
        }

        public void Store(TaskData taskData)
        {
            if (taskData == null || string.IsNullOrEmpty(taskData.TaskId)) return;

            if (Data.Tasks.ContainsKey(taskData.TaskId))
                RemoveFromIndex(taskData.TaskId);

            Data.Tasks[taskData.TaskId] = taskData;
            IndexTask(taskData);
            TraceModify();
        }

        public void StoreBatch(IEnumerable<TaskData> tasks)
        {
            if (tasks == null) return;

            foreach (var task in tasks)
                Store(task);
        }

        public void Update(TaskData taskData)
        {
            if (taskData == null || string.IsNullOrEmpty(taskData.TaskId)) return;
            if (!Data.Tasks.ContainsKey(taskData.TaskId)) return;

            Data.Tasks[taskData.TaskId] = taskData;
            TraceModify();
        }

        public void Remove(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            if (!Data.Tasks.Remove(taskId)) return;

            RemoveFromIndex(taskId);
            TraceModify();
        }

        public void Clear()
        {
            Data.Tasks.Clear();
            Data.ConditionIndex.Clear();
            TraceModify();
        }

        public Dictionary<string, TaskSaveData> ExportProgress()
        {
            var result = new Dictionary<string, TaskSaveData>();

            foreach (var task in Data.Tasks.Values)
            {
                var saveData = new TaskSaveData { State = task.State };
                if (task.Conditions != null)
                {
                    foreach (var condition in task.Conditions)
                        saveData.ConditionProgress[condition.ConditionId] = condition.CurrentValue;
                }

                result[task.TaskId] = saveData;
            }

            return result;
        }

        public void ImportProgress(Dictionary<string, TaskSaveData> data)
        {
            if (data == null) return;

            foreach (var pair in data)
            {
                if (!Data.Tasks.TryGetValue(pair.Key, out var task)) continue;

                task.State = pair.Value.State;
                if (task.Conditions == null || pair.Value.ConditionProgress == null) continue;

                foreach (var condition in task.Conditions)
                {
                    if (pair.Value.ConditionProgress.TryGetValue(condition.ConditionId, out var value))
                        condition.CurrentValue = value;
                }
            }

            TraceModify();
        }

        private void IndexTask(TaskData task)
        {
            if (task?.Conditions == null) return;

            foreach (var condition in task.Conditions)
            {
                var key = (condition.Type, condition.Param);
                if (!Data.ConditionIndex.TryGetValue(key, out var list))
                {
                    list = new List<(string taskId, string conditionId)>();
                    Data.ConditionIndex[key] = list;
                }

                list.Add((task.TaskId, condition.ConditionId));
            }
        }

        private void RemoveFromIndex(string taskId)
        {
            foreach (var list in Data.ConditionIndex.Values)
                list.RemoveAll(entry => entry.taskId == taskId);
        }
    }
}
