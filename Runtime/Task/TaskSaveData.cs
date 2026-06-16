using System;
using System.Collections.Generic;

namespace JulyGame.Task
{
    [Serializable]
    public class TaskStateSave
    {
        public int taskId;
        public int state;
    }

    [Serializable]
    public class TaskBoundarySave
    {
        public int taskId;
        public long ticks;
    }

    [Serializable]
    public class TaskSaveBundle
    {
        public List<TaskStateSave> states = new();
        public List<TaskBoundarySave> resetBoundaries = new();
    }
}
