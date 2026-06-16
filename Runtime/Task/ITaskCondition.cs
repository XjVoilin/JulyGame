namespace JulyGame.Task
{
    public interface ITaskCondition
    {
        int ConditionId { get; }
        bool IsCompleted { get; }
        float Progress { get; }
    }
}
