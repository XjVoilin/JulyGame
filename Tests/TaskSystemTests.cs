using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyGame.Task;
using NUnit.Framework;
using UnityEngine;

namespace JulyGame.Tests.Task
{
    /// <summary>
    /// TaskSystemBase 集成测试，同时充当最小接入示例：
    /// 展示如何实现 <see cref="ITaskCondition"/> / <see cref="ITaskUnlockRule"/> /
    /// <see cref="ITaskResetPolicy"/>，并继承 <see cref="TaskSystemBase"/> 注册任务。
    /// push 驱动：条件/规则变化后调用 notifier，基座同步评估。
    /// </summary>
    [TestFixture]
    public class TaskSystemTests
    {
        #region Sample extension-point implementations

        /// <summary>计数型条件：达到目标值即完成，Progress 线性反映完成度。</summary>
        private sealed class CountCondition : ITaskCondition
        {
            public int ConditionId { get; }
            public int Target;

            private int _current;
            private Action _onChanged;

            public CountCondition(int id, int target)
            {
                ConditionId = id;
                Target = target;
            }

            public int Current
            {
                get => _current;
                set
                {
                    if (_current == value) return;
                    _current = value;
                    _onChanged?.Invoke();
                }
            }

            public bool IsCompleted => _current >= Target;
            public float Progress => Target <= 0 ? 1f : Mathf.Clamp01((float)_current / Target);
            public void Reset() => _current = 0;
            public void BindChangeNotifier(Action onChanged) => _onChanged = onChanged;
        }

        /// <summary>
        /// 进度恒定但完成态可翻转的条件，用于验证完成检测以 IsCompleted 为权威、
        /// 而非依赖 Progress 跨过 1。
        /// </summary>
        private sealed class StuckProgressCondition : ITaskCondition
        {
            public int ConditionId { get; }

            private bool _done;
            private Action _onChanged;

            public StuckProgressCondition(int id) => ConditionId = id;

            public bool Done
            {
                get => _done;
                set
                {
                    if (_done == value) return;
                    _done = value;
                    _onChanged?.Invoke();
                }
            }

            public bool IsCompleted => _done;
            public float Progress => 0.5f;
            public void Reset() => _done = false;
            public void BindChangeNotifier(Action onChanged) => _onChanged = onChanged;
        }

        private sealed class FlagRule : ITaskUnlockRule
        {
            private bool _allow;
            private Action _onChanged;

            public bool Allow
            {
                get => _allow;
                set
                {
                    if (_allow == value) return;
                    _allow = value;
                    _onChanged?.Invoke();
                }
            }

            public bool CanUnlock() => _allow;
            public void BindChangeNotifier(Action onChanged) => _onChanged = onChanged;
        }

        /// <summary>每日 0 点（UTC）重置。</summary>
        private sealed class DailyResetPolicy : ITaskResetPolicy
        {
            public DateTime GetNextResetUtc(DateTime utcNow) => utcNow.Date.AddDays(1);
            public void BindChangeNotifier(Action onChanged) { }
        }

        /// <summary>最小接入示例：把待注册任务交给基座，并暴露可控时间。</summary>
        private sealed class TestTaskSystem : TaskSystemBase
        {
            public DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            public readonly List<TaskData> ToRegister = new();

            protected override void OnConfigure()
            {
                foreach (var t in ToRegister)
                    RegisterTask(t);
            }

            protected override DateTime OnGetUtcNow() => Now;
        }

        #endregion

        private ArchContext _ctx;
        private TestTaskSystem _system;

        private void Boot(params TaskData[] tasks)
        {
            _ctx = new ArchContext();
            _ctx.RegisterStore(new TaskStore());

            _system = new TestTaskSystem();
            _system.ToRegister.AddRange(tasks);
            _ctx.RegisterSystem(_system);

            _ctx.InitializeAsync().GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            _ctx?.Shutdown();
            _ctx = null;
            _system = null;
        }

        private static TaskData Task(int id, ETaskState state, ITaskCondition cond,
            ITaskUnlockRule rule = null, ITaskResetPolicy policy = null)
        {
            var t = new TaskData { TaskId = id, State = state };
            if (cond != null) t.Conditions.Add(cond);
            if (rule != null) t.UnlockRules = new List<ITaskUnlockRule> { rule };
            t.ResetPolicy = policy;
            return t;
        }

        [Test]
        public void Locked_NoRules_UnlocksOnRegister()
        {
            Boot(Task(1, ETaskState.Locked, new CountCondition(1, 3)));

            // Locked + no rules → 注册时立即解锁
            Assert.AreEqual(ETaskState.InProgress, _system.GetTask(1).State);
        }

        [Test]
        public void Locked_WithRule_StaysLockedUntilRuleMet()
        {
            var rule = new FlagRule { Allow = false };
            Boot(Task(1, ETaskState.Locked, new CountCondition(1, 3), rule));

            Assert.AreEqual(ETaskState.Locked, _system.GetTask(1).State);

            // 翻转规则 → notifier 触发 → 基座同步评估解锁
            rule.Allow = true;
            Assert.AreEqual(ETaskState.InProgress, _system.GetTask(1).State);
        }

        [Test]
        public void Progress_Change_PublishesProgressUpdated()
        {
            var cond = new CountCondition(1, 4);
            Boot(Task(1, ETaskState.InProgress, cond));

            TaskProgressUpdatedEvent captured = default;
            var count = 0;
            _ctx.Event.Subscribe<TaskProgressUpdatedEvent>(e => { captured = e; count++; }, this);

            // 直接改条件 → notifier 触发 → 基座同步评估
            cond.Current = 2;

            Assert.AreEqual(1, count);
            Assert.AreEqual(0.5f, captured.NewProgress, 1e-4f);
            Assert.IsFalse(captured.ConditionJustCompleted);
        }

        [Test]
        public void AllConditionsMet_CompletesTask()
        {
            var cond = new CountCondition(1, 2);
            Boot(Task(1, ETaskState.InProgress, cond));

            var completed = false;
            _ctx.Event.Subscribe<TaskCompletedEvent>(e => completed = true, this);

            cond.Current = 2;

            Assert.AreEqual(ETaskState.Completed, _system.GetTask(1).State);
            Assert.IsTrue(completed);
        }

        [Test]
        public void ConditionCompleted_FiresEvenWhenProgressDoesNotMove()
        {
            var cond = new StuckProgressCondition(1);
            Boot(Task(1, ETaskState.InProgress, cond));

            var conditionCompleted = false;
            _ctx.Event.Subscribe<TaskConditionCompletedEvent>(e => conditionCompleted = true, this);

            cond.Done = true;

            Assert.IsTrue(conditionCompleted, "完成检测必须以 IsCompleted 为权威，不依赖 Progress 跨过 1");
            Assert.AreEqual(ETaskState.Completed, _system.GetTask(1).State);
        }

        [Test]
        public void DailyReset_CrossingBoundary_ResetsStateAndConditionProgress()
        {
            var cond = new CountCondition(1, 2);
            Boot(Task(1, ETaskState.InProgress, cond, policy: new DailyResetPolicy()));

            cond.Current = 2;
            Assert.AreEqual(ETaskState.Completed, _system.GetTask(1).State);

            _system.Now = new DateTime(2026, 1, 2, 0, 30, 0, DateTimeKind.Utc);
            _system.SweepResets();

            Assert.AreEqual(ETaskState.InProgress, _system.GetTask(1).State, "跨越每日边界后应回到进行中");
            Assert.AreEqual(0, cond.Current, "重置必须清零条件计数");
        }

        [Test]
        public void ManualReset_ClearsConditions()
        {
            var cond = new CountCondition(1, 2);
            Boot(Task(1, ETaskState.InProgress, cond));

            cond.Current = 2;
            Assert.AreEqual(ETaskState.Completed, _system.GetTask(1).State);

            var ok = _system.ResetTask(1);

            Assert.IsTrue(ok);
            Assert.AreEqual(ETaskState.InProgress, _system.GetTask(1).State);
            Assert.AreEqual(0, cond.Current);
        }

        [Test]
        public void UnregisterTask_RemovesAndPublishes()
        {
            Boot(Task(1, ETaskState.InProgress, new CountCondition(1, 2)));

            var removedId = -1;
            _ctx.Event.Subscribe<TaskRemovedEvent>(e => removedId = e.TaskId, this);

            var ok = _system.UnregisterTask(1);

            Assert.IsTrue(ok);
            Assert.IsNull(_system.GetTask(1));
            Assert.AreEqual(1, removedId);
            Assert.IsFalse(_system.UnregisterTask(1), "重复移除应返回 false");
        }

        [Test]
        public void SaveLoad_RoundTripsState()
        {
            var cond = new CountCondition(1, 2);
            Boot(Task(1, ETaskState.InProgress, cond));

            cond.Current = 2;
            Assert.AreEqual(ETaskState.Completed, _system.GetTask(1).State);

            var bundle = _system.ExportData();

            // 重建一套系统，状态应从存档恢复为 Completed。
            _ctx.Shutdown();
            Boot(Task(1, ETaskState.InProgress, new CountCondition(1, 2)));
            _system.ImportData(bundle);

            Assert.AreEqual(ETaskState.Completed, _system.GetTask(1).State);
        }

        [Test]
        public void Reentrancy_RegisterDuringCompletionEvent_DoesNotThrow()
        {
            var cond = new CountCondition(1, 1);
            Boot(Task(1, ETaskState.InProgress, cond));

            _ctx.Event.Subscribe<TaskCompletedEvent>(_ =>
            {
                _system.RegisterTask(Task(99, ETaskState.InProgress, new CountCondition(1, 1)));
            }, this);

            cond.Current = 1;
            Assert.IsNotNull(_system.GetTask(99));
        }
    }
}
