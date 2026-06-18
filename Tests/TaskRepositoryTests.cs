using System.Collections.Generic;
using JulyGame.Task;
using NUnit.Framework;

namespace JulyGame.Tests.Task
{
    [TestFixture]
    public class TaskRepositoryTests
    {
        private static TaskData Task(int id, ETaskState state)
            => new TaskData { TaskId = id, State = state };

        [Test]
        public void Add_ThenGet_ReturnsTask()
        {
            var repo = new TaskRepository();
            var t = Task(1, ETaskState.Locked);

            repo.Add(t);

            Assert.AreSame(t, repo.Get(1));
            Assert.AreEqual(1, repo.All.Count);
        }

        [Test]
        public void Add_CreatesLiveBundleEntry()
        {
            var repo = new TaskRepository();
            repo.Add(Task(1, ETaskState.InProgress));

            Assert.AreEqual(1, repo.Bundle.states.Count);
            Assert.AreEqual(1, repo.Bundle.states[0].taskId);
            Assert.AreEqual((int)ETaskState.InProgress, repo.Bundle.states[0].state);
        }

        [Test]
        public void SetState_MovesBucketIndex()
        {
            var repo = new TaskRepository();
            repo.Add(Task(1, ETaskState.Locked));

            repo.SetState(1, ETaskState.InProgress);

            Assert.AreEqual(0, repo.GetIdsByState(ETaskState.Locked).Count);
            CollectionAssert.Contains(repo.GetIdsByState(ETaskState.InProgress), 1);
        }

        [Test]
        public void SetState_SyncsToBundleEntry()
        {
            var repo = new TaskRepository();
            repo.Add(Task(1, ETaskState.Locked));

            repo.SetState(1, ETaskState.Completed);

            Assert.AreEqual((int)ETaskState.Completed, repo.Bundle.states[0].state,
                "SetState 应同步更新 bundle 条目");
        }

        [Test]
        public void SetResetBoundary_SyncsToBundleEntry()
        {
            var repo = new TaskRepository();
            repo.Add(Task(1, ETaskState.InProgress));

            repo.SetResetBoundary(1, 999L);

            Assert.AreEqual(1, repo.Bundle.resetBoundaries.Count);
            Assert.AreEqual(999L, repo.Bundle.resetBoundaries[0].ticks);
        }

        [Test]
        public void Remove_DropsTaskBoundaryAndBucket()
        {
            var repo = new TaskRepository();
            repo.Add(Task(1, ETaskState.InProgress));
            repo.SetResetBoundary(1, 123L);

            Assert.IsTrue(repo.Remove(1));

            Assert.IsNull(repo.Get(1));
            Assert.AreEqual(0, repo.GetResetBoundary(1));
            Assert.AreEqual(0, repo.GetIdsByState(ETaskState.InProgress).Count);
            Assert.AreEqual(0, repo.Bundle.states.Count, "Remove 应同步清理 bundle");
            Assert.AreEqual(0, repo.Bundle.resetBoundaries.Count);
            Assert.IsFalse(repo.Remove(1), "重复移除应返回 false");
        }

        [Test]
        public void ConstructorWithBundle_RestoresStateOnAdd()
        {
            var bundle = new TaskSaveBundle();
            bundle.states.Add(new TaskStateSave { taskId = 1, state = (int)ETaskState.Completed });
            bundle.resetBoundaries.Add(new TaskBoundarySave { taskId = 1, ticks = 999L });

            var repo = new TaskRepository(bundle);
            repo.Add(Task(1, ETaskState.Locked));

            Assert.AreEqual(ETaskState.Completed, repo.Get(1).State,
                "Add 应从 bundle 恢复已有存档状态");
            Assert.AreEqual(999L, repo.GetResetBoundary(1),
                "Add 应从 bundle 恢复已有边界");
            Assert.AreSame(bundle, repo.Bundle,
                "Bundle 应为构造时传入的同一引用");
        }

        [Test]
        public void WriteOps_TriggerMarkDirty()
        {
            int dirtyCount = 0;
            var repo = new TaskRepository(new TaskSaveBundle(), () => dirtyCount++);

            repo.Add(Task(1, ETaskState.Locked));
            Assert.AreEqual(1, dirtyCount, "Add 应触发 markDirty");

            repo.SetState(1, ETaskState.InProgress);
            Assert.AreEqual(2, dirtyCount, "SetState 应触发 markDirty");

            repo.SetResetBoundary(1, 100L);
            Assert.AreEqual(3, dirtyCount, "SetResetBoundary 应触发 markDirty");

            repo.Remove(1);
            Assert.AreEqual(4, dirtyCount, "Remove 应触发 markDirty");
        }

        [Test]
        public void Import_AppliesExternalBundleAndSyncsInternal()
        {
            var repo = new TaskRepository();
            repo.Add(Task(1, ETaskState.Locked));

            var external = new TaskSaveBundle();
            external.states.Add(new TaskStateSave { taskId = 1, state = (int)ETaskState.Completed });
            repo.Import(external);

            Assert.AreEqual(ETaskState.Completed, repo.Get(1).State);
            Assert.AreEqual((int)ETaskState.Completed, repo.Bundle.states[0].state,
                "Import 应同步更新内部 bundle 条目");
        }
    }
}
