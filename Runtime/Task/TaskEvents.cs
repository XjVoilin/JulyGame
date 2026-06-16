namespace JulyGame.Task
{
    public struct TaskRegisteredEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    public struct TaskUnlockedEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    public struct TaskProgressUpdatedEvent
    {
        public int TaskId;
        public int ConditionId;
        public float OldProgress;
        public float NewProgress;
        public bool ConditionJustCompleted;
        public bool TaskJustCompleted;
    }

    public struct TaskConditionCompletedEvent
    {
        public int TaskId;
        public int ConditionId;
    }

    public struct TaskCompletedEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }

    public struct TaskStateChangedEvent
    {
        public int TaskId;
        public ETaskState OldState;
        public ETaskState NewState;
        public TaskData TaskData;
    }

    public struct TaskResetEvent
    {
        public int TaskId;
        public TaskData TaskData;
    }
}
