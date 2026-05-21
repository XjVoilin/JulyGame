using System;

namespace JulyGame.Task
{
    public interface ITaskTypeHandler : IDisposable
    {
        TaskType TaskType { get; }
        void SetContext(ITaskHandlerContext context);
        void OnRegister();
        void OnTaskUnlocked(TaskData taskData);
    }

    public interface ITaskHandlerContext
    {
        JulyEvents.IEventBus EventBus { get; }
        void UpdateProgress(TaskConditionType conditionType, string param, int delta = 1);
        void UpdateTaskProgress(string taskId, string conditionId, int value);
        TaskData GetTask(string taskId);
    }
}
