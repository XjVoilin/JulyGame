using System;

namespace JulyGame.Task
{
    public interface ITaskResetPolicy
    {
        DateTime GetNextResetUtc(DateTime utcNow);
    }
}
