using System;
using System.Collections.Generic;
using JulyArch;
using UnityEngine;

namespace JulyGame.Task
{
    public abstract class TaskSystemBase : GameSystemBase, IUpdatableSystem
    {
        private TaskStore _store;
        private float _evalAccumulator;

        private readonly Dictionary<long, float> _lastProgress = new();

        protected sealed override void OnInitialize()
        {
            _store = GetStore<TaskStore>();
        }

        protected sealed override void OnStart()
        {
            OnConfigure();
        }

        protected sealed override void OnShutdown()
        {
            OnDispose();
            _lastProgress.Clear();
        }

        protected abstract void OnConfigure();
        protected virtual void OnDispose() { }
        protected virtual DateTime OnGetUtcNow() => DateTime.UtcNow;
        protected virtual float OnGetEvalIntervalSeconds() => 0f;

        #region Registration

        public void RegisterTask(TaskData task)
        {
            if (task == null) return;
            _store.Add(task);

            if (task.ResetPolicy != null)
            {
                var boundary = task.ResetPolicy.GetNextResetUtc(OnGetUtcNow());
                _store.SetResetBoundary(task.TaskId, boundary.Ticks);
            }

            Publish(new TaskRegisteredEvent { TaskId = task.TaskId, TaskData = task });

            if (task.State == ETaskState.InProgress)
                CacheConditionProgress(task);
        }

        #endregion

        #region State Machine (escape hatches)

        public bool UnlockTask(int id)
        {
            var task = _store.Get(id);
            if (task == null || task.State != ETaskState.Locked) return false;

            TransitionToInProgress(task);
            return true;
        }

        public bool ResetTask(int id)
        {
            var task = _store.Get(id);
            if (task == null || task.State != ETaskState.Completed) return false;

            var oldState = task.State;
            _store.SetState(id, ETaskState.InProgress);
            CacheConditionProgress(task);

            Publish(new TaskStateChangedEvent
            {
                TaskId = id, OldState = oldState,
                NewState = ETaskState.InProgress, TaskData = task
            });
            Publish(new TaskResetEvent { TaskId = id, TaskData = task });

            return true;
        }

        #endregion

        #region Queries

        public TaskData GetTask(int id) => _store.Get(id);

        public IEnumerable<TaskData> GetAll() => _store.All.Values;

        public DateTime? GetNextResetUtc(int id)
        {
            var task = _store.Get(id);
            if (task?.ResetPolicy == null) return null;
            var ticks = _store.GetResetBoundary(id);
            return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;
        }

        #endregion

        #region Save / Load

        public TaskSaveBundle ExportProgress()
        {
            var bundle = new TaskSaveBundle();
            foreach (var pair in _store.All)
            {
                bundle.states.Add(new TaskStateSave
                {
                    taskId = pair.Key,
                    state = (int)pair.Value.State
                });

                var ticks = _store.GetResetBoundary(pair.Key);
                if (ticks > 0)
                {
                    bundle.resetBoundaries.Add(new TaskBoundarySave
                    {
                        taskId = pair.Key,
                        ticks = ticks
                    });
                }
            }

            return bundle;
        }

        public void ImportProgress(TaskSaveBundle bundle)
        {
            if (bundle == null) return;

            var stateDict = new Dictionary<int, ETaskState>(bundle.states.Count);
            foreach (var s in bundle.states)
                stateDict[s.taskId] = (ETaskState)s.state;
            _store.ImportStates(stateDict);

            var boundaryDict = new Dictionary<int, long>(bundle.resetBoundaries.Count);
            foreach (var b in bundle.resetBoundaries)
                boundaryDict[b.taskId] = b.ticks;
            _store.ImportResetBoundaries(boundaryDict);

            foreach (var pair in _store.All)
            {
                if (pair.Value.State == ETaskState.InProgress)
                    CacheConditionProgress(pair.Value);
            }
        }

        #endregion

        #region OnUpdate (per-tick evaluation)

        public void OnUpdate(float deltaTime)
        {
            var interval = OnGetEvalIntervalSeconds();
            if (interval > 0f)
            {
                _evalAccumulator += deltaTime;
                if (_evalAccumulator < interval) return;
                _evalAccumulator = 0f;
            }

            EvaluateLockedTasks();
            EvaluateInProgressTasks();
            EvaluateResetPolicies();
        }

        private void EvaluateLockedTasks()
        {
            var lockedIds = _store.GetIdsByState(ETaskState.Locked);
            for (var i = lockedIds.Count - 1; i >= 0; i--)
            {
                var task = _store.Get(lockedIds[i]);
                if (task == null) continue;

                if (CanUnlockByRules(task))
                    TransitionToInProgress(task);
            }
        }

        private void EvaluateInProgressTasks()
        {
            var inProgressIds = _store.GetIdsByState(ETaskState.InProgress);
            for (var i = inProgressIds.Count - 1; i >= 0; i--)
            {
                var task = _store.Get(inProgressIds[i]);
                if (task == null || task.Conditions == null) continue;

                var allCompleted = true;
                for (var c = 0; c < task.Conditions.Count; c++)
                {
                    var cond = task.Conditions[c];
                    var newProgress = cond.Progress;
                    var cacheKey = PackKey(task.TaskId, cond.ConditionId);
                    _lastProgress.TryGetValue(cacheKey, out var oldProgress);

                    var justCompleted = false;
                    if (Math.Abs(newProgress - oldProgress) > 1e-6f)
                    {
                        _lastProgress[cacheKey] = newProgress;
                        justCompleted = !WasCompleted(oldProgress) && cond.IsCompleted;

                        Publish(new TaskProgressUpdatedEvent
                        {
                            TaskId = task.TaskId,
                            ConditionId = cond.ConditionId,
                            OldProgress = oldProgress,
                            NewProgress = newProgress,
                            ConditionJustCompleted = justCompleted,
                            TaskJustCompleted = false
                        });

                        if (justCompleted)
                        {
                            Publish(new TaskConditionCompletedEvent
                            {
                                TaskId = task.TaskId,
                                ConditionId = cond.ConditionId
                            });
                        }
                    }

                    if (!cond.IsCompleted)
                        allCompleted = false;
                }

                if (allCompleted && task.Conditions.Count > 0)
                {
                    _store.SetState(task.TaskId, ETaskState.Completed);

                    Publish(new TaskStateChangedEvent
                    {
                        TaskId = task.TaskId, OldState = ETaskState.InProgress,
                        NewState = ETaskState.Completed, TaskData = task
                    });
                    Publish(new TaskCompletedEvent { TaskId = task.TaskId, TaskData = task });
                }
            }
        }

        private void EvaluateResetPolicies()
        {
            foreach (var pair in _store.All)
            {
                var task = pair.Value;
                if (task.ResetPolicy == null) continue;

                var now = OnGetUtcNow();
                var boundary = task.ResetPolicy.GetNextResetUtc(now);
                var savedTicks = _store.GetResetBoundary(task.TaskId);

                if (savedTicks == 0)
                {
                    _store.SetResetBoundary(task.TaskId, boundary.Ticks);
                    continue;
                }

                if (boundary.Ticks != savedTicks)
                {
                    _store.SetResetBoundary(task.TaskId, boundary.Ticks);

                    if (task.State == ETaskState.Completed || task.State == ETaskState.InProgress)
                    {
                        var oldState = task.State;
                        _store.SetState(task.TaskId, ETaskState.InProgress);
                        CacheConditionProgress(task);

                        if (oldState != ETaskState.InProgress)
                        {
                            Publish(new TaskStateChangedEvent
                            {
                                TaskId = task.TaskId, OldState = oldState,
                                NewState = ETaskState.InProgress, TaskData = task
                            });
                        }

                        Publish(new TaskResetEvent { TaskId = task.TaskId, TaskData = task });
                    }
                }
            }
        }

        #endregion

        #region Private helpers

        private bool CanUnlockByRules(TaskData task)
        {
            if (task.UnlockRules == null || task.UnlockRules.Count == 0)
                return true;

            for (var i = 0; i < task.UnlockRules.Count; i++)
            {
                try
                {
                    if (!task.UnlockRules[i].CanUnlock())
                        return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TaskSystem] UnlockRule evaluation failed for task {task.TaskId}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        private void TransitionToInProgress(TaskData task)
        {
            _store.SetState(task.TaskId, ETaskState.InProgress);
            CacheConditionProgress(task);

            Publish(new TaskStateChangedEvent
            {
                TaskId = task.TaskId, OldState = ETaskState.Locked,
                NewState = ETaskState.InProgress, TaskData = task
            });
            Publish(new TaskUnlockedEvent { TaskId = task.TaskId, TaskData = task });
        }

        private void CacheConditionProgress(TaskData task)
        {
            if (task.Conditions == null) return;

            for (var i = 0; i < task.Conditions.Count; i++)
            {
                var cond = task.Conditions[i];
                _lastProgress[PackKey(task.TaskId, cond.ConditionId)] = cond.Progress;
            }
        }

        private static long PackKey(int taskId, int conditionId)
        {
            return ((long)taskId << 32) | (uint)conditionId;
        }

        private static bool WasCompleted(float progress)
        {
            return progress >= 1f - 1e-6f;
        }

        #endregion
    }
}
