using System;
using System.Collections.Generic;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务运行时数据容器：任务字典 + 按状态分桶的 id 索引 + 重置边界 Ticks。
    /// 内部维护一个可序列化的 <see cref="TaskSaveBundle"/>，所有写操作同步更新它。
    /// 项目 Store 持有 <see cref="Bundle"/> 引用即可落盘，无需 Export 重建。
    /// </summary>
    public sealed class TaskRepository
    {
        private readonly Dictionary<int, TaskData> _tasks = new();
        private readonly Dictionary<ETaskState, List<int>> _idsByState = new()
        {
            { ETaskState.Locked, new List<int>() },
            { ETaskState.InProgress, new List<int>() },
            { ETaskState.Completed, new List<int>() }
        };
        private readonly Dictionary<int, long> _resetBoundaryTicks = new();

        private readonly TaskSaveBundle _bundle;
        private readonly Action _markDirty;
        private readonly Dictionary<int, TaskStateSave> _stateIndex = new();
        private readonly Dictionary<int, TaskBoundarySave> _boundaryIndex = new();

        /// <summary>可直接序列化的状态包，始终与运行时同步。项目 Store 持有此引用即可落盘。</summary>
        public TaskSaveBundle Bundle => _bundle;

        public IReadOnlyDictionary<int, TaskData> All => _tasks;

        public TaskRepository() : this(new TaskSaveBundle(), null) { }

        /// <summary>以已有存档包为后备存储构造。Add 时自动恢复已有状态。</summary>
        public TaskRepository(TaskSaveBundle bundle) : this(bundle, null) { }

        public TaskRepository(TaskSaveBundle bundle, Action markDirty)
        {
            _bundle = bundle ?? new TaskSaveBundle();
            _markDirty = markDirty;
            foreach (var s in _bundle.states)
                _stateIndex[s.taskId] = s;
            foreach (var b in _bundle.resetBoundaries)
                _boundaryIndex[b.taskId] = b;
        }

        public TaskData Get(int taskId)
        {
            _tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public IReadOnlyList<int> GetIdsByState(ETaskState state)
            => _idsByState.TryGetValue(state, out var list) ? list : Array.Empty<int>();

        public long GetResetBoundary(int taskId)
        {
            _resetBoundaryTicks.TryGetValue(taskId, out var ticks);
            return ticks;
        }

        /// <summary>
        /// 注册任务。若 bundle 中已有该 id 的存档状态，会自动恢复到 task 上；
        /// 否则以 task 当前状态新建存档条目。
        /// </summary>
        public void Add(TaskData task)
        {
            if (task == null) return;

            if (_stateIndex.TryGetValue(task.TaskId, out var saved))
            {
                task.State = (ETaskState)saved.state;
            }
            else
            {
                var entry = new TaskStateSave { taskId = task.TaskId, state = (int)task.State };
                _bundle.states.Add(entry);
                _stateIndex[task.TaskId] = entry;
            }

            if (_boundaryIndex.TryGetValue(task.TaskId, out var savedBoundary))
                _resetBoundaryTicks[task.TaskId] = savedBoundary.ticks;

            _tasks[task.TaskId] = task;
            AddToBucket(task.TaskId, task.State);
            _markDirty?.Invoke();
        }

        public bool Remove(int taskId)
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return false;

            RemoveFromBucket(taskId, task.State);
            _tasks.Remove(taskId);
            _resetBoundaryTicks.Remove(taskId);

            if (_stateIndex.Remove(taskId, out var stateEntry))
                _bundle.states.Remove(stateEntry);
            if (_boundaryIndex.Remove(taskId, out var boundaryEntry))
                _bundle.resetBoundaries.Remove(boundaryEntry);

            _markDirty?.Invoke();
            return true;
        }

        public void SetState(int taskId, ETaskState newState)
        {
            if (!_tasks.TryGetValue(taskId, out var task)) return;

            var oldState = task.State;
            if (oldState == newState) return;

            RemoveFromBucket(taskId, oldState);
            task.State = newState;
            AddToBucket(taskId, newState);

            if (_stateIndex.TryGetValue(taskId, out var save))
                save.state = (int)newState;

            _markDirty?.Invoke();
        }

        public void SetResetBoundary(int taskId, long ticks)
        {
            _resetBoundaryTicks[taskId] = ticks;

            if (_boundaryIndex.TryGetValue(taskId, out var save))
            {
                save.ticks = ticks;
            }
            else
            {
                var entry = new TaskBoundarySave { taskId = taskId, ticks = ticks };
                _bundle.resetBoundaries.Add(entry);
                _boundaryIndex[taskId] = entry;
            }

            _markDirty?.Invoke();
        }

        /// <summary>返回内部维护的存档包引用（始终最新）。</summary>
        public TaskSaveBundle Export() => _bundle;

        /// <summary>从外部数据包恢复状态与边界到运行时，并同步到内部存档包。需在任务已 Add 之后调用。</summary>
        public void Import(TaskSaveBundle bundle)
        {
            if (bundle == null) return;

            if (bundle.states != null)
            {
                foreach (var s in bundle.states)
                {
                    if (!_tasks.TryGetValue(s.taskId, out var task)) continue;

                    var newState = (ETaskState)s.state;
                    var oldState = task.State;
                    if (oldState == newState) continue;

                    RemoveFromBucket(s.taskId, oldState);
                    task.State = newState;
                    AddToBucket(s.taskId, newState);

                    if (_stateIndex.TryGetValue(s.taskId, out var internalSave))
                        internalSave.state = s.state;
                }
            }

            if (bundle.resetBoundaries != null)
            {
                foreach (var b in bundle.resetBoundaries)
                {
                    _resetBoundaryTicks[b.taskId] = b.ticks;

                    if (_boundaryIndex.TryGetValue(b.taskId, out var internalSave))
                        internalSave.ticks = b.ticks;
                    else
                        SetResetBoundary(b.taskId, b.ticks);
                }
            }

            _markDirty?.Invoke();
        }

        private void AddToBucket(int taskId, ETaskState state)
        {
            if (!_idsByState.TryGetValue(state, out var list))
            {
                list = new List<int>();
                _idsByState[state] = list;
            }
            list.Add(taskId);
        }

        private void RemoveFromBucket(int taskId, ETaskState state)
        {
            if (_idsByState.TryGetValue(state, out var list))
                list.Remove(taskId);
        }
    }
}
