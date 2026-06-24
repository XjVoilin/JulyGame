using System;
using System.Collections.Generic;

namespace JulyGame
{
    [Serializable]
    public class HttpPendingQueueData : ISaveData
    {
        public SaveImportance Importance => SaveImportance.Critical;
        public List<HttpPendingEntry> Entries = new();
    }

    [Serializable]
    public class HttpPendingEntry
    {
        public string Path;
        public string Body;
    }
}
