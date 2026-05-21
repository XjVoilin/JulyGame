using System;
using System.Collections.Generic;
using System.Linq;
using JulyArch;

namespace JulyGame.ABTest
{
    public class ABTestStoreData
    {
        public Dictionary<string, Experiment> Experiments = new();
        public Dictionary<string, UserExperimentAssignment> Assignments = new();
        public string UserId;
        public string DeviceId;
        public Dictionary<string, string> UserAttributes = new();
    }

    public class ABTestStore : StoreBase<ABTestStoreData>
    {
        #region Experiments

        public void StoreExperiment(Experiment experiment)
        {
            if (experiment == null || string.IsNullOrEmpty(experiment.ExperimentId))
                return;

            Data.Experiments[experiment.ExperimentId] = experiment;
            TraceModify();
        }

        public void StoreExperiments(IEnumerable<Experiment> experiments)
        {
            if (experiments == null) return;

            foreach (var experiment in experiments)
            {
                if (experiment != null && !string.IsNullOrEmpty(experiment.ExperimentId))
                    Data.Experiments[experiment.ExperimentId] = experiment;
            }

            TraceModify();
        }

        public bool RemoveExperiment(string experimentId)
        {
            var removed = !string.IsNullOrEmpty(experimentId) && Data.Experiments.Remove(experimentId);
            if (removed) TraceModify();
            return removed;
        }

        public void ClearExperiments()
        {
            Data.Experiments.Clear();
            TraceModify();
        }

        public Experiment GetExperiment(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return null;
            return Data.Experiments.GetValueOrDefault(experimentId);
        }

        public List<Experiment> GetAllExperiments() => Data.Experiments.Values.ToList();

        public List<Experiment> GetRunningExperiments()
        {
            return Data.Experiments.Values.Where(e => e.Status == ExperimentStatus.Running).ToList();
        }

        public bool UpdateExperiment(Experiment experiment)
        {
            if (experiment == null || string.IsNullOrEmpty(experiment.ExperimentId))
                return false;

            if (!Data.Experiments.ContainsKey(experiment.ExperimentId))
                return false;

            Data.Experiments[experiment.ExperimentId] = experiment;
            TraceModify();
            return true;
        }

        #endregion

        #region Assignments

        public void StoreAssignment(UserExperimentAssignment assignment)
        {
            if (assignment == null || string.IsNullOrEmpty(assignment.ExperimentId))
                return;

            Data.Assignments[assignment.ExperimentId] = assignment;
            TraceModify();
        }

        public UserExperimentAssignment GetAssignment(string experimentId)
        {
            if (string.IsNullOrEmpty(experimentId)) return null;
            return Data.Assignments.GetValueOrDefault(experimentId);
        }

        public bool RemoveAssignment(string experimentId)
        {
            var removed = !string.IsNullOrEmpty(experimentId) && Data.Assignments.Remove(experimentId);
            if (removed) TraceModify();
            return removed;
        }

        public void ClearAssignments()
        {
            Data.Assignments.Clear();
            TraceModify();
        }

        #endregion

        #region User identity & attributes

        public void SetUserId(string userId)
        {
            Data.UserId = userId;
            TraceModify();
        }

        public void SetDeviceId(string deviceId)
        {
            Data.DeviceId = deviceId;
            TraceModify();
        }

        public void SetUserAttribute(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            Data.UserAttributes[key] = value;
            TraceModify();
        }

        public void SetUserAttributes(Dictionary<string, string> attributes)
        {
            if (attributes == null) return;

            foreach (var kvp in attributes)
                Data.UserAttributes[kvp.Key] = kvp.Value;

            TraceModify();
        }

        public void ClearUserAttributes()
        {
            Data.UserAttributes.Clear();
            TraceModify();
        }

        public string GetUserId() => Data.UserId;
        public string GetDeviceId() => Data.DeviceId;
        public IReadOnlyDictionary<string, string> GetUserAttributes() => Data.UserAttributes;

        #endregion

        #region Import / export

        public ABTestSaveData ExportSaveData()
        {
            return new ABTestSaveData
            {
                UserId = Data.UserId,
                Assignments = new Dictionary<string, UserExperimentAssignment>(Data.Assignments)
            };
        }

        public void ImportSaveData(ABTestSaveData saveData)
        {
            if (saveData == null) return;

            if (!string.IsNullOrEmpty(saveData.UserId))
                Data.UserId = saveData.UserId;

            Data.Assignments.Clear();
            if (saveData.Assignments != null)
            {
                foreach (var kvp in saveData.Assignments)
                    Data.Assignments[kvp.Key] = kvp.Value;
            }

            TraceModify();
        }

        #endregion
    }
}
