using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore;
using JulyCore.Core;
using JulyCore.Data.Save;

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
    /// </summary>
    public class ActivityStore : StoreBase<ActivityStoreData>, IAsyncLoadable
    {
        private ActivityRuntimeData _saveData;

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
            SyncToSaveData();
            GF.Save.MarkDirty(Frameworkconst.ActivitySaveKey);
            TraceModify();
        }

        internal void MarkOpened(string activityId)
        {
            if (string.IsNullOrEmpty(activityId)) return;

            if (Data.OpenedActivityIds.Add(activityId))
            {
                SyncToSaveData();
                GF.Save.MarkDirty(Frameworkconst.ActivitySaveKey);
                TraceModify();
            }
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

            if (!changed) return;

            SyncToSaveData();
            GF.Save.MarkDirty(Frameworkconst.ActivitySaveKey);
            TraceModify();
        }

        async UniTask IAsyncLoadable.OnLoadAsync()
        {
            Data = new ActivityStoreData();
            _saveData = await GF.Save.LoadAndRegisterAsync<ActivityRuntimeData>(Frameworkconst.ActivitySaveKey);
            SyncFromSaveData();
        }

        protected override void OnShutdown()
        {
            GF.Save.Unregister(Frameworkconst.ActivitySaveKey);
            _saveData = null;
        }

        private void SyncFromSaveData()
        {
            if (_saveData == null) return;

            Data.Records.Clear();
            foreach (var pair in _saveData.RecordMap)
                Data.Records[pair.Key] = pair.Value;

            Data.OpenedActivityIds.Clear();
            foreach (var activityId in _saveData.OpenedActivityIds)
                Data.OpenedActivityIds.Add(activityId);
        }

        private void SyncToSaveData()
        {
            if (_saveData == null) return;

            _saveData.RecordMap.Clear();
            foreach (var pair in Data.Records)
                _saveData.RecordMap[pair.Key] = pair.Value;

            _saveData.OpenedActivityIds.Clear();
            foreach (var activityId in Data.OpenedActivityIds)
                _saveData.OpenedActivityIds.Add(activityId);
        }
    }
}
