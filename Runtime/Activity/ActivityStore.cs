using System.Collections.Generic;
using JulyArch;

namespace JulyGame.Activity
{
    public class ActivityStoreData
    {
        public Dictionary<string, ActivityDefinition> Definitions = new();
        public Dictionary<string, ActivityRecord> Records = new();
        public HashSet<string> OpenedActivityIds = new();
        public Dictionary<string, ActivityState> StateCache = new();
    }

    /// <summary>
    /// 活动数据 Store：持有活动定义、运行时记录、开启标记与状态缓存。
    /// 纯内存 Store，持久化由项目层经 ActivitySystemBase.ExportData/ImportData 负责。
    /// </summary>
    public class ActivityStore : StoreBase<ActivityStoreData>
    {
        public IReadOnlyDictionary<string, ActivityDefinition> Definitions => Data.Definitions;
        public IReadOnlyDictionary<string, ActivityRecord> Records => Data.Records;
        public IReadOnlyCollection<string> OpenedActivityIds => Data.OpenedActivityIds;
        public IReadOnlyDictionary<string, ActivityState> StateCache => Data.StateCache;

        public bool TryGetDefinition(string activityId, out ActivityDefinition definition)
        {
            return Data.Definitions.TryGetValue(activityId, out definition);
        }

        public bool TryGetRecord(string activityId, out ActivityRecord record)
        {
            return Data.Records.TryGetValue(activityId, out record);
        }

        public bool TryGetCachedState(string activityId, out ActivityState state)
        {
            return Data.StateCache.TryGetValue(activityId, out state);
        }

        public bool IsActivityOpened(string activityId)
        {
            return !string.IsNullOrEmpty(activityId) && Data.OpenedActivityIds.Contains(activityId);
        }

        internal void RegisterDefinition(ActivityDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id)) return;

            Data.Definitions[definition.Id] = definition;
            TraceModify();
        }

        internal bool UnregisterDefinition(string activityId)
        {
            if (string.IsNullOrEmpty(activityId)) return false;

            var removed = Data.Definitions.Remove(activityId);
            Data.StateCache.Remove(activityId);
            if (removed)
                TraceModify();

            return removed;
        }

        internal ActivityRecord GetOrCreateRecord(string activityId)
        {
            if (Data.Records.TryGetValue(activityId, out var record))
                return record;

            record = new ActivityRecord { ActivityId = activityId };
            Data.Records[activityId] = record;
            TraceModify();
            return record;
        }

        internal void UpdateRecord(ActivityRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ActivityId)) return;

            Data.Records[record.ActivityId] = record;
            TraceModify();
        }

        internal void MarkOpened(string activityId)
        {
            if (string.IsNullOrEmpty(activityId)) return;

            if (Data.OpenedActivityIds.Add(activityId))
                TraceModify();
        }

        internal void SetCachedState(string activityId, ActivityState state)
        {
            if (string.IsNullOrEmpty(activityId)) return;

            Data.StateCache[activityId] = state;
            TraceModify();
        }

        internal void ClearActivityData(string activityId)
        {
            if (string.IsNullOrEmpty(activityId)) return;

            var changed = Data.Records.Remove(activityId);
            if (Data.OpenedActivityIds.Remove(activityId))
                changed = true;

            if (changed)
                TraceModify();
        }

        /// <summary>导出可持久化的运行时数据（记录 + 已开启标记）。</summary>
        internal ActivityRuntimeData ExportRuntime()
        {
            var data = new ActivityRuntimeData();
            foreach (var pair in Data.Records)
                data.RecordMap[pair.Key] = pair.Value;
            foreach (var id in Data.OpenedActivityIds)
                data.OpenedActivityIds.Add(id);
            return data;
        }

        /// <summary>从数据包恢复运行时数据。覆盖现有记录与开启标记。</summary>
        internal void ImportRuntime(ActivityRuntimeData data)
        {
            if (data == null) return;

            Data.Records.Clear();
            if (data.RecordMap != null)
            {
                foreach (var pair in data.RecordMap)
                    Data.Records[pair.Key] = pair.Value;
            }

            Data.OpenedActivityIds.Clear();
            if (data.OpenedActivityIds != null)
            {
                foreach (var id in data.OpenedActivityIds)
                    Data.OpenedActivityIds.Add(id);
            }

            TraceModify();
        }
    }
}
