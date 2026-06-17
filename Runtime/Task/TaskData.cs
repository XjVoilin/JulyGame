using System.Collections.Generic;

namespace JulyGame.Task
{
    /// <summary>
    /// 任务运行时数据。纯数据骨架，不含业务语义（无奖励、无分类、无 UI）。
    /// 接入方通过组合 <see cref="Conditions"/> / <see cref="UnlockRules"/> / <see cref="ResetPolicy"/>
    /// 三个扩展点来表达具体任务，再交给 <see cref="TaskSystemBase.RegisterTask"/> 托管。
    /// </summary>
    public class TaskData
    {
        /// <summary>全局唯一任务标识。</summary>
        public int TaskId;

        /// <summary>当前状态。初始为 <see cref="ETaskState.Locked"/>，由基座驱动流转。</summary>
        public ETaskState State = ETaskState.Locked;

        /// <summary>完成条件集合（逻辑与：全部 <see cref="ITaskCondition.IsCompleted"/> 才算任务完成）。</summary>
        public List<ITaskCondition> Conditions = new();

        /// <summary>解锁规则集合（逻辑与）。为 null 或空表示无前置，注册后立即可解锁。</summary>
        public List<ITaskUnlockRule> UnlockRules;

        /// <summary>重置策略。为 null 表示一次性任务，完成后不再重置。</summary>
        public ITaskResetPolicy ResetPolicy;

        /// <summary>是否所有条件均已达成（无条件视为已达成）。以 <see cref="ITaskCondition.IsCompleted"/> 为准。</summary>
        public bool AreAllConditionsCompleted()
        {
            if (Conditions == null || Conditions.Count == 0)
                return true;

            for (var i = 0; i < Conditions.Count; i++)
            {
                if (!Conditions[i].IsCompleted)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 展示用总体进度 [0,1]，取各条件 <see cref="ITaskCondition.Progress"/> 的均值。
        /// 无条件任务则按状态返回：已完成为 1，否则为 0。
        /// </summary>
        public float GetOverallProgress()
        {
            if (Conditions == null || Conditions.Count == 0)
                return State >= ETaskState.Completed ? 1f : 0f;

            var total = 0f;
            for (var i = 0; i < Conditions.Count; i++)
                total += Conditions[i].Progress;

            return total / Conditions.Count;
        }
    }
}
