using System;
using System.Collections.Generic;

namespace JulyGame.Task
{
    /// <summary>单个任务的状态存档项。state 为 <see cref="ETaskState"/> 的整数值。</summary>
    [Serializable]
    public class TaskStateSave
    {
        public int taskId;
        public int state;
    }

    /// <summary>单个任务的重置边界存档项。ticks 为下一次重置的 UTC <see cref="System.DateTime.Ticks"/>。</summary>
    [Serializable]
    public class TaskBoundarySave
    {
        public int taskId;
        public long ticks;
    }

    /// <summary>
    /// 任务进度存档包。持久化中立：基座只产出/消费此纯数据结构，
    /// 具体落盘方案（PlayerPrefs、文件、服务器）由接入方决定。
    /// </summary>
    [Serializable]
    public class TaskSaveBundle
    {
        public List<TaskStateSave> states = new();
        public List<TaskBoundarySave> resetBoundaries = new();
    }
}
