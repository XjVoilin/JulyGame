using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCommon;
using UnityEngine;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务系统基座。提供"解锁 → 进行 → 完成 → 重置"的通用状态机骨架，不含任何业务语义。
    /// 接入方继承本类，在 <see cref="OnConfigure"/> 中通过 <see cref="RegisterTask"/> 注册由
    /// <see cref="ITaskCondition"/> / <see cref="ITaskUnlockRule"/> / <see cref="ITaskResetPolicy"/>
    /// 组合而成的任务，并订阅 TaskXxxEvent 接管发奖、UI 等表现层逻辑。
    /// </summary>
    /// <remarks>
    /// 评估模型（push 驱动）：条件与解锁规则在注册时被注入变更通知回调
    /// （<see cref="ITaskCondition.BindChangeNotifier"/> /
    /// <see cref="ITaskUnlockRule.BindChangeNotifier"/>），
    /// 内部状态变化后由接入方主动调用回调，基座收到通知后同步评估**单个任务**的状态流转。
    /// 不做每帧全量轮询，进度变更到事件广播零延迟。
    /// <para/>
    /// 重置为时间驱动、批量检查：接入方在自身时间源（App 心跳、前台恢复等）中调用
    /// <see cref="SweepResets"/>，基座据 <see cref="ITaskResetPolicy"/> 的边界统一判定与重置。
    /// </remarks>
    public abstract class TaskSystemBase : SystemBase
    {
        private TaskRepository _repo;

        // 条件缓存：进度用于差量检测触发 TaskProgressUpdatedEvent，完成态用于检测条件完成边沿
        // 触发 TaskConditionCompletedEvent。键 = PackKey(taskId, conditionId)。
        private readonly Dictionary<long, CondCache> _condCache = new();

        // push 静默标志：基座主动调 Reset/CacheConditionState 期间置 true，忽略扩展点的 notifier 回调。
        private bool _muted;

        // 单任务重入保护：评估某任务期间若同任务再次 push，置 pending 并在本次结束后补跑。
        private bool _evaluating;
        private int _evaluatingTaskId;
        private bool _evaluatingPending;

        /// <summary>单个条件的展示进度 + 完成态缓存。</summary>
        private readonly struct CondCache
        {
            public readonly float Progress;
            public readonly bool Completed;

            public CondCache(float progress, bool completed)
            {
                Progress = progress;
                Completed = completed;
            }
        }

        protected sealed override UniTask OnInitializeAsync()
        {
            _repo = ResolveRepository();
            return UniTask.CompletedTask;
        }

        protected sealed override void OnPostInitialize()
        {
            OnConfigure();
        }

        protected sealed override void OnShutdown()
        {
            OnDispose();

            foreach (var pair in _repo.All)
                ClearExtensionSubscriptions(pair.Value);

            _condCache.Clear();
        }

        /// <summary>接入方返回本模块唯一的任务数据容器（通常持有于项目 Store 中）。</summary>
        protected abstract TaskRepository ResolveRepository();

        /// <summary>接入方在此注册任务。基座启动时调用一次。</summary>
        protected abstract void OnConfigure();

        /// <summary>接入方清理钩子。基座关闭时调用一次。</summary>
        protected virtual void OnDispose() { }

        /// <summary>UTC 时间源。可覆写以接入服务器对时或便于测试。</summary>
        protected virtual DateTime OnGetUtcNow() => DateTime.UtcNow;

        #region Registration

        /// <summary>
        /// 注册一个任务交由基座托管。重复 TaskId 会覆盖。
        /// 注册时基座会：绑定条件/规则的 notifier、计算重置边界、对 Locked 立即 TryUnlock、
        /// 对 InProgress 缓存条件状态并做一次完成检查。
        /// </summary>
        public void RegisterTask(TaskData task)
        {
            if (task == null) return;
            _repo.Add(task);

            BindNotifiers(task);

            if (task.ResetPolicy != null && _repo.GetResetBoundary(task.TaskId) == 0)
            {
                var boundary = task.ResetPolicy.GetNextResetUtc(OnGetUtcNow());
                _repo.SetResetBoundary(task.TaskId, boundary.Ticks);
            }

            SyncExtensionActivation(task);

            Publish(new TaskRegisteredEvent { TaskId = task.TaskId, TaskData = task });

            if (task.State == ETaskState.Locked)
            {
                TryUnlock(task);
            }
            else if (task.State == ETaskState.InProgress)
            {
                _muted = true;
                CacheConditionState(task);
                _muted = false;
                EvaluateInProgressTask(task);
            }
        }

        /// <summary>
        /// 从系统中移除任务，并清理其状态索引、重置边界与进度/完成缓存。
        /// </summary>
        /// <returns>任务存在并被移除返回 true。</returns>
        public bool UnregisterTask(int id)
        {
            var task = _repo.Get(id);
            if (task == null) return false;

            ClearExtensionSubscriptions(task);
            ClearConditionCache(task);

            if (!_repo.Remove(id)) return false;

            Publish(new TaskRemovedEvent { TaskId = id });
            return true;
        }

        #endregion

        #region State Machine (escape hatches)

        /// <summary>手动重置已完成任务回到 InProgress，并清零其条件进度。仅对 Completed 任务有效。</summary>
        public bool ResetTask(int id)
        {
            var task = _repo.Get(id);
            if (task == null || task.State != ETaskState.Completed) return false;

            _repo.SetState(id, ETaskState.InProgress);
            ResetConditions(task);
            SyncExtensionActivation(task);

            Publish(new TaskStateChangedEvent
            {
                TaskId = id, OldState = ETaskState.Completed,
                NewState = ETaskState.InProgress, TaskData = task
            });
            Publish(new TaskResetEvent { TaskId = id, TaskData = task });

            EvaluateInProgressTask(task);

            return true;
        }

        #endregion

        #region Queries

        public TaskData GetTask(int id) => _repo.Get(id);

        public IEnumerable<TaskData> GetAll() => _repo.All.Values;

        /// <summary>下一次重置的 UTC 时间；无重置策略返回 null。</summary>
        public DateTime? GetNextResetUtc(int id)
        {
            var task = _repo.Get(id);
            if (task?.ResetPolicy == null) return null;
            var ticks = _repo.GetResetBoundary(id);
            return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;
        }

        #endregion

        #region Save / Load

        /// <summary>从数据包恢复任务状态与重置边界，并按状态对齐扩展点激活。需在任务注册完成后调用。</summary>
        public void ImportData(TaskSaveBundle bundle)
        {
            if (bundle == null) return;

            _repo.Import(bundle);

            _muted = true;
            foreach (var pair in _repo.All)
            {
                SyncExtensionActivation(pair.Value);

                if (pair.Value.State == ETaskState.InProgress)
                    CacheConditionState(pair.Value);
            }
            _muted = false;
        }

        #endregion

        #region SweepResets (explicit batch check)

        /// <summary>
        /// 批量检查所有带重置策略的任务是否跨越了重置边界，跨越则执行重置。
        /// 由接入方在合适时机（前台恢复、App 心跳等）显式调用。
        /// </summary>
        public void SweepResets()
        {
            var now = OnGetUtcNow();

            // 先快照 id：CheckAndResetTask 会改状态，不能在遍历 _repo.All 时直接重置。
            var ids = new List<int>();
            foreach (var pair in _repo.All)
            {
                if (pair.Value.ResetPolicy != null)
                    ids.Add(pair.Key);
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var task = _repo.Get(ids[i]);
                if (task?.ResetPolicy == null) continue;

                CheckAndResetTask(task, now);
            }
        }

        #endregion

        #region Push evaluation (condition / unlock / reset)

        private void OnConditionChanged(int taskId)
        {
            if (_muted) return;

            var task = _repo.Get(taskId);
            if (task == null || task.State != ETaskState.InProgress) return;

            if (_evaluating && _evaluatingTaskId == taskId)
            {
                _evaluatingPending = true;
                return;
            }

            EvaluateInProgressTask(task);
        }

        private void OnUnlockRuleChanged(int taskId)
        {
            if (_muted) return;

            var task = _repo.Get(taskId);
            if (task == null || task.State != ETaskState.Locked) return;

            TryUnlock(task);
        }

        /// <summary>评估单个 InProgress 任务：差量检测进度/完成，发事件，全部完成则流转。</summary>
        private void EvaluateInProgressTask(TaskData task)
        {
            _evaluating = true;
            _evaluatingTaskId = task.TaskId;
            _evaluatingPending = false;

            try
            {
                EvaluateInProgressTaskCore(task);
            }
            finally
            {
                _evaluating = false;
            }

            if (_evaluatingPending)
            {
                _evaluatingPending = false;
                task = _repo.Get(task.TaskId);
                if (task != null && task.State == ETaskState.InProgress)
                    EvaluateInProgressTask(task);
            }
        }

        private void EvaluateInProgressTaskCore(TaskData task)
        {
            if (task.Conditions == null) return;

            var allCompleted = true;
            for (var c = 0; c < task.Conditions.Count; c++)
            {
                var cond = task.Conditions[c];
                var cacheKey = PackKey(task.TaskId, cond.ConditionId);

                var newProgress = cond.Progress;
                var isCompleted = cond.IsCompleted;

                _condCache.TryGetValue(cacheKey, out var cached);
                var oldProgress = cached.Progress;
                var wasCompleted = cached.Completed;

                var justCompleted = isCompleted && !wasCompleted;
                var progressChanged = Math.Abs(newProgress - oldProgress) > 1e-6f;

                if (progressChanged || justCompleted)
                {
                    Publish(new TaskProgressUpdatedEvent
                    {
                        TaskId = task.TaskId,
                        ConditionId = cond.ConditionId,
                        OldProgress = oldProgress,
                        NewProgress = newProgress,
                        ConditionJustCompleted = justCompleted
                    });
                }

                if (progressChanged || isCompleted != wasCompleted)
                    _condCache[cacheKey] = new CondCache(newProgress, isCompleted);

                if (justCompleted)
                {
                    Publish(new TaskConditionCompletedEvent
                    {
                        TaskId = task.TaskId,
                        ConditionId = cond.ConditionId
                    });
                }

                if (!isCompleted)
                    allCompleted = false;
            }

            if (allCompleted && task.Conditions.Count > 0)
            {
                _repo.SetState(task.TaskId, ETaskState.Completed);
                SyncExtensionActivation(task);

                Publish(new TaskStateChangedEvent
                {
                    TaskId = task.TaskId, OldState = ETaskState.InProgress,
                    NewState = ETaskState.Completed, TaskData = task
                });
                Publish(new TaskCompletedEvent { TaskId = task.TaskId, TaskData = task });
            }
        }

        /// <summary>检查 Locked 任务是否满足所有解锁规则，满足则流转到 InProgress。</summary>
        private void TryUnlock(TaskData task)
        {
            if (!CanUnlockByRules(task)) return;

            TransitionToInProgress(task);
        }

        /// <summary>检查单个任务的重置边界是否已跨越，跨越则执行重置。</summary>
        private void CheckAndResetTask(TaskData task, DateTime utcNow)
        {
            var boundary = task.ResetPolicy.GetNextResetUtc(utcNow);
            var savedTicks = _repo.GetResetBoundary(task.TaskId);

            if (savedTicks == 0)
            {
                _repo.SetResetBoundary(task.TaskId, boundary.Ticks);
                return;
            }

            if (boundary.Ticks != savedTicks)
            {
                _repo.SetResetBoundary(task.TaskId, boundary.Ticks);

                if (task.State == ETaskState.Completed || task.State == ETaskState.InProgress)
                {
                    var oldState = task.State;
                    _repo.SetState(task.TaskId, ETaskState.InProgress);
                    ResetConditions(task);
                    SyncExtensionActivation(task);

                    if (oldState != ETaskState.InProgress)
                    {
                        Publish(new TaskStateChangedEvent
                        {
                            TaskId = task.TaskId, OldState = oldState,
                            NewState = ETaskState.InProgress, TaskData = task
                        });
                    }

                    Publish(new TaskResetEvent { TaskId = task.TaskId, TaskData = task });

                    EvaluateInProgressTask(task);
                }
            }
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// 按任务当前状态同步扩展点的激活/休眠。幂等，重复调用安全。
        /// Condition 当且仅当 InProgress 活动；UnlockRule 当且仅当 Locked 活动。
        /// ResetPolicy 不参与激活窗口，仅在 SweepResets 时被查询。
        /// </summary>
        private void SyncExtensionActivation(TaskData task)
        {
            SetConditionsActive(task, task.State == ETaskState.InProgress);
            SetRulesActive(task, task.State == ETaskState.Locked);
        }

        private void SetConditionsActive(TaskData task, bool active)
        {
            if (task.Conditions == null) return;
            for (var i = 0; i < task.Conditions.Count; i++)
            {
                if (task.Conditions[i] is TaskConditionBase cb)
                {
                    if (active) cb.Activate();
                    else cb.Deactivate();
                }
            }
        }

        private void SetRulesActive(TaskData task, bool active)
        {
            if (task.UnlockRules == null) return;
            for (var i = 0; i < task.UnlockRules.Count; i++)
            {
                if (task.UnlockRules[i] is TaskUnlockRuleBase rb)
                {
                    if (active) rb.Activate();
                    else rb.Deactivate();
                }
            }
        }

        private void BindNotifiers(TaskData task)
        {
            var taskId = task.TaskId;

            if (task.Conditions != null)
            {
                for (var i = 0; i < task.Conditions.Count; i++)
                {
                    var id = taskId;
                    task.Conditions[i].BindChangeNotifier(() => OnConditionChanged(id));
                }
            }

            if (task.UnlockRules != null)
            {
                for (var i = 0; i < task.UnlockRules.Count; i++)
                {
                    var id = taskId;
                    task.UnlockRules[i].BindChangeNotifier(() => OnUnlockRuleChanged(id));
                }
            }
        }

        /// <summary>
        /// 清理任务中继承了辅助基类的条件/解锁规则订阅的游戏事件。
        /// 直接实现接口而未继承 Base 的扩展点由接入方自行负责清理。
        /// </summary>
        private void ClearExtensionSubscriptions(TaskData task)
        {
            if (task.Conditions != null)
            {
                for (var i = 0; i < task.Conditions.Count; i++)
                    (task.Conditions[i] as TaskConditionBase)?.ClearSubscriptions();
            }

            if (task.UnlockRules != null)
            {
                for (var i = 0; i < task.UnlockRules.Count; i++)
                    (task.UnlockRules[i] as TaskUnlockRuleBase)?.ClearSubscriptions();
            }
        }

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
                    JLogger.LogError($"[TaskSystem] UnlockRule evaluation failed for task {task.TaskId}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        private void TransitionToInProgress(TaskData task)
        {
            _repo.SetState(task.TaskId, ETaskState.InProgress);
            ResetConditions(task);
            SyncExtensionActivation(task);

            Publish(new TaskStateChangedEvent
            {
                TaskId = task.TaskId, OldState = ETaskState.Locked,
                NewState = ETaskState.InProgress, TaskData = task
            });
            Publish(new TaskUnlockedEvent { TaskId = task.TaskId, TaskData = task });

            EvaluateInProgressTask(task);
        }

        /// <summary>调用各条件的 Reset() 清零内部计数，再以清零后的值重建缓存。静默期内 notifier 被忽略。</summary>
        private void ResetConditions(TaskData task)
        {
            if (task.Conditions == null) return;

            _muted = true;
            for (var i = 0; i < task.Conditions.Count; i++)
            {
                var cond = task.Conditions[i];
                try { cond.Reset(); }
                catch (Exception ex)
                {
                    JLogger.LogError($"[TaskSystem] Condition.Reset failed for task {task.TaskId} cond {cond.ConditionId}: {ex.Message}");
                }
            }

            CacheConditionState(task);
            _muted = false;
        }

        /// <summary>以各条件当前值重建进度与完成缓存（不调用 Reset）。</summary>
        private void CacheConditionState(TaskData task)
        {
            if (task.Conditions == null) return;

            for (var i = 0; i < task.Conditions.Count; i++)
            {
                var cond = task.Conditions[i];
                var key = PackKey(task.TaskId, cond.ConditionId);
                _condCache[key] = new CondCache(cond.Progress, cond.IsCompleted);
            }
        }

        /// <summary>移除任务时清理其全部条件的缓存条目。</summary>
        private void ClearConditionCache(TaskData task)
        {
            if (task.Conditions == null) return;

            for (var i = 0; i < task.Conditions.Count; i++)
            {
                var key = PackKey(task.TaskId, task.Conditions[i].ConditionId);
                _condCache.Remove(key);
            }
        }

        private static long PackKey(int taskId, int conditionId)
        {
            return ((long)taskId << 32) | (uint)conditionId;
        }

        #endregion
    }
}
