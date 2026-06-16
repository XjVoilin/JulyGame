using System.Collections.Generic;

namespace JulyGame.Task
{
    public class TaskData
    {
        public int TaskId;
        public ETaskState State = ETaskState.Locked;
        public List<ITaskCondition> Conditions = new();
        public List<ITaskUnlockRule> UnlockRules;
        public ITaskResetPolicy ResetPolicy;

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
