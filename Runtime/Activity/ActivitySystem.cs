using System.Collections.Generic;
using System.Linq;
using JulyArch;
using JulyCore;

namespace JulyGame.Activity
{
    public abstract class ActivitySystemBase : GameSystemBase, IUpdatableSystem
    {
        private const float StateCheckInterval = 60f;

        private ActivityStore _store;
        private readonly HashSet<string> _newlyOpenedIds = new();
        private float _lastStateCheckTime;
        private bool _isReady;

        protected sealed override void OnInitialize()
        {
            _store = GetStore<ActivityStore>();
            _isReady = false;
            _lastStateCheckTime = 0f;
        }

        protected sealed override void OnStart()
        {
            OnConfigure();
        }

        protected sealed override void OnShutdown()
        {
            OnDispose();
            _newlyOpenedIds.Clear();
            _isReady = false;
        }

        protected abstract void OnConfigure();
        protected virtual void OnDispose() { }

        public void OnUpdate(float deltaTime)
        {
            if (!_isReady || _store.Definitions.Count == 0)
                return;

            _lastStateCheckTime += GF.Time.UnscaledDeltaTime;
            if (_lastStateCheckTime >= StateCheckInterval)
            {
                _lastStateCheckTime = 0f;
                CheckAndUpdateStates();
            }
        }

        public void RegisterActivity(ActivityDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return;

            _store.RegisterDefinition(definition);
            _store.SetCachedState(
                definition.Id,
                CalculateState(definition.PreAnnounceTime, definition.StartTime, definition.EndTime));
        }

        public void RegisterActivities(IEnumerable<ActivityDefinition> definitions)
        {
            if (definitions == null) return;

            foreach (var definition in definitions)
                RegisterActivity(definition);
        }

        public bool UnregisterActivity(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
                return false;

            _newlyOpenedIds.Remove(activityId);
            return _store.UnregisterDefinition(activityId);
        }

        public void CompleteRegistration()
        {
            if (_isReady)
                return;

            ProcessActivityStates();
            _isReady = true;

            Publish(new ActivityRegisteredEvent
            {
                RegisteredCount = _store.Definitions.Count
            });

            var activeCount = _store.StateCache.Values.Count(state => state == ActivityState.InProgress);
            Publish(new ActivityModuleReadyEvent
            {
                ActiveCount = activeCount,
                NewlyOpenedCount = _newlyOpenedIds.Count
            });
        }

        public List<ActivityInfo> GetAllActivities()
        {
            var result = new List<ActivityInfo>(_store.Definitions.Count);
            foreach (var pair in _store.Definitions)
                result.Add(BuildActivityInfo(pair.Key, pair.Value));

            return result.OrderBy(info => info.Definition.Priority).ToList();
        }

        public ActivityInfo GetActivity(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
                return null;

            return _store.TryGetDefinition(activityId, out var definition)
                ? BuildActivityInfo(activityId, definition)
                : null;
        }

        public List<ActivityInfo> GetActivitiesByType(int type)
        {
            return GetAllActivities().Where(info => info.Definition.Type == type).ToList();
        }

        public List<ActivityInfo> GetActivitiesByState(ActivityState state)
        {
            return GetAllActivities().Where(info => info.State == state).ToList();
        }

        public ActivityState GetActivityState(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
                return ActivityState.NotStarted;

            if (_store.TryGetCachedState(activityId, out var state))
                return state;

            if (_store.TryGetDefinition(activityId, out var definition))
            {
                state = CalculateState(definition.PreAnnounceTime, definition.StartTime, definition.EndTime);
                _store.SetCachedState(activityId, state);
                return state;
            }

            return ActivityState.NotStarted;
        }

        public bool HasActivity(string activityId)
        {
            return !string.IsNullOrEmpty(activityId) && _store.Definitions.ContainsKey(activityId);
        }

        public ActivityState CalculateState(long preAnnounceTime, long startTime, long endTime)
        {
            var now = GF.Time.ServerTimeUtcTimestamp;

            if (now > endTime)
                return ActivityState.Ended;

            if (now >= startTime)
                return ActivityState.InProgress;

            if (preAnnounceTime > 0 && preAnnounceTime < startTime && now >= preAnnounceTime)
                return ActivityState.PreAnnounce;

            return ActivityState.NotStarted;
        }

        public ActivityState CalculateState(long startTime, long endTime)
        {
            return CalculateState(0, startTime, endTime);
        }

        public ActivityRecord GetActivityRecord(string activityId)
        {
            return _store.TryGetRecord(activityId, out var record) ? record : null;
        }

        public void SaveProgressData(string activityId, string dataPayload)
        {
            if (string.IsNullOrEmpty(activityId))
                return;

            var record = _store.GetOrCreateRecord(activityId);
            record.DataPayload = dataPayload;
            record.LastUpdateTime = GF.Time.ServerTimeUtcTimestamp;
            _store.UpdateRecord(record);

            Publish(new ActivityProgressChangedEvent
            {
                ActivityId = activityId,
                Record = record
            });
        }

        public string GetProgressData(string activityId)
        {
            return GetActivityRecord(activityId)?.DataPayload;
        }

        public void ClearActivityData(string activityId)
        {
            _store.ClearActivityData(activityId);
        }

        private void CheckAndUpdateStates()
        {
            foreach (var pair in _store.Definitions)
            {
                var activityId = pair.Key;
                var definition = pair.Value;
                var oldState = _store.TryGetCachedState(activityId, out var cachedState)
                    ? cachedState
                    : ActivityState.NotStarted;
                var newState = CalculateState(definition.PreAnnounceTime, definition.StartTime, definition.EndTime);

                if (oldState == newState)
                    continue;

                _store.SetCachedState(activityId, newState);
                OnActivityStateChanged(activityId, definition, oldState, newState);
            }
        }

        private void ProcessActivityStates()
        {
            foreach (var pair in _store.Definitions)
            {
                var activityId = pair.Key;
                var definition = pair.Value;
                var state = _store.TryGetCachedState(activityId, out var cachedState)
                    ? cachedState
                    : ActivityState.NotStarted;

                if (state != ActivityState.InProgress || _store.IsActivityOpened(activityId))
                    continue;

                _newlyOpenedIds.Add(activityId);
                _store.MarkOpened(activityId);
                Publish(new ActivityOpenedEvent
                {
                    ActivityId = activityId,
                    Definition = definition
                });
            }
        }

        private void OnActivityStateChanged(
            string activityId,
            ActivityDefinition definition,
            ActivityState oldState,
            ActivityState newState)
        {
            if (newState == ActivityState.InProgress &&
                (oldState == ActivityState.NotStarted || oldState == ActivityState.PreAnnounce))
            {
                if (!_store.IsActivityOpened(activityId))
                {
                    _newlyOpenedIds.Add(activityId);
                    _store.MarkOpened(activityId);
                }

                Publish(new ActivityOpenedEvent
                {
                    ActivityId = activityId,
                    Definition = definition
                });
            }
            else if (newState == ActivityState.Ended)
            {
                _newlyOpenedIds.Remove(activityId);
                Publish(new ActivityClosedEvent
                {
                    ActivityId = activityId,
                    Definition = definition
                });
            }
        }

        private ActivityInfo BuildActivityInfo(string activityId, ActivityDefinition definition)
        {
            return new ActivityInfo
            {
                Definition = definition,
                State = GetActivityState(activityId),
                Record = GetActivityRecord(activityId),
                IsNewlyOpened = _newlyOpenedIds.Contains(activityId)
            };
        }
    }
}
