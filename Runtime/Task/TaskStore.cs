using System;
using System.Collections.Generic;
using JulyArch;

namespace JulyGame.Task
{
    public class TaskStoreData
    {
        public readonly Dictionary<int, TaskData> Tasks = new();
        public readonly Dictionary<ETaskState, List<int>> TaskIdsByState = new()
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
            return Data.TaskIdsByState.TryGetValue(state, out var list) ? list : Array.Empty<int>();
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
            AddToTaskIdsByState(task.TaskId, task.State);
            TraceModify();
        }

        internal void SetState(int taskId, ETaskState newState)
        {
            if (!Data.Tasks.TryGetValue(taskId, out var task)) return;

            var oldState = task.State;
            if (oldState == newState) return;

            RemoveFromTaskIdsByState(taskId, oldState);
            task.State = newState;
            AddToTaskIdsByState(taskId, newState);
            TraceModify();
        }

        internal void SetResetBoundary(int taskId, long ticks)
        {
            Data.ResetBoundaryTicks[taskId] = ticks;
            TraceModify();
        }

        internal void ImportStates(List<TaskStateSave> states)
        {
            if (states == null) return;

            foreach (var s in states)
            {
                if (!Data.Tasks.TryGetValue(s.taskId, out var task)) continue;

                var newState = (ETaskState)s.state;
                var oldState = task.State;
                if (oldState == newState) continue;

                RemoveFromTaskIdsByState(s.taskId, oldState);
                task.State = newState;
                AddToTaskIdsByState(s.taskId, newState);
            }

            TraceModify();
        }

        internal void ImportResetBoundaries(List<TaskBoundarySave> boundaries)
        {
            if (boundaries == null) return;

            foreach (var b in boundaries)
                Data.ResetBoundaryTicks[b.taskId] = b.ticks;

            TraceModify();
        }

        private void AddToTaskIdsByState(int taskId, ETaskState state)
        {
            if (!Data.TaskIdsByState.TryGetValue(state, out var list))
            {
                list = new List<int>();
                Data.TaskIdsByState[state] = list;
            }

            list.Add(taskId);
        }

        private void RemoveFromTaskIdsByState(int taskId, ETaskState state)
        {
            if (Data.TaskIdsByState.TryGetValue(state, out var list))
                list.Remove(taskId);
        }
    }
}
