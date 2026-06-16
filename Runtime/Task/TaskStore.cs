using System;
using System.Collections.Generic;
using JulyArch;

namespace JulyGame.Task
{
    public class TaskStoreData
    {
        public readonly Dictionary<int, TaskData> Tasks = new();
        public readonly Dictionary<ETaskState, List<int>> StateIndex = new()
        {
            { ETaskState.Locked, new List<int>() },
            { ETaskState.InProgress, new List<int>() },
            { ETaskState.Completed, new List<int>() }
        };
        public readonly Dictionary<int, long> ResetBoundaryTicks = new();
    }

    public class TaskStore : StoreBase<TaskStoreData>
    {
        public TaskData Get(int taskId)
        {
            Data.Tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public IReadOnlyDictionary<int, TaskData> All => Data.Tasks;

        public IReadOnlyList<int> GetIdsByState(ETaskState state)
        {
            return Data.StateIndex.TryGetValue(state, out var list) ? list : Array.Empty<int>();
        }

        public long GetResetBoundary(int taskId)
        {
            Data.ResetBoundaryTicks.TryGetValue(taskId, out var ticks);
            return ticks;
        }

        internal void Add(TaskData task)
        {
            if (task == null) return;
            Data.Tasks[task.TaskId] = task;
            AddToStateIndex(task.TaskId, task.State);
            TraceModify();
        }

        internal void SetState(int taskId, ETaskState newState)
        {
            if (!Data.Tasks.TryGetValue(taskId, out var task)) return;

            var oldState = task.State;
            if (oldState == newState) return;

            RemoveFromStateIndex(taskId, oldState);
            task.State = newState;
            AddToStateIndex(taskId, newState);
            TraceModify();
        }

        internal void SetResetBoundary(int taskId, long ticks)
        {
            Data.ResetBoundaryTicks[taskId] = ticks;
            TraceModify();
        }

        internal void ImportStates(Dictionary<int, ETaskState> states)
        {
            if (states == null) return;

            foreach (var pair in states)
            {
                if (!Data.Tasks.TryGetValue(pair.Key, out var task)) continue;

                var oldState = task.State;
                if (oldState == pair.Value) continue;

                RemoveFromStateIndex(pair.Key, oldState);
                task.State = pair.Value;
                AddToStateIndex(pair.Key, pair.Value);
            }

            TraceModify();
        }

        internal void ImportResetBoundaries(Dictionary<int, long> boundaries)
        {
            if (boundaries == null) return;

            foreach (var pair in boundaries)
                Data.ResetBoundaryTicks[pair.Key] = pair.Value;

            TraceModify();
        }

        private void AddToStateIndex(int taskId, ETaskState state)
        {
            if (!Data.StateIndex.TryGetValue(state, out var list))
            {
                list = new List<int>();
                Data.StateIndex[state] = list;
            }

            list.Add(taskId);
        }

        private void RemoveFromStateIndex(int taskId, ETaskState state)
        {
            if (Data.StateIndex.TryGetValue(state, out var list))
                list.Remove(taskId);
        }
    }
}
