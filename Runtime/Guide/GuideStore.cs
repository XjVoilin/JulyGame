using System.Collections.Generic;
using System.Linq;
using JulyArch;

namespace JulyGame.Guide
{
    public class GuideStoreData
    {
        public HashSet<string> CompletedFlows = new();
        public HashSet<string> CompletedSteps = new();
        public string CurrentFlowId;
        public string CurrentStepId;
    }

    public class GuideStore : StoreBase<GuideStoreData>
    {
        public bool IsFlowCompleted(string flowId)
        {
            return !string.IsNullOrEmpty(flowId) && Data.CompletedFlows.Contains(flowId);
        }

        public bool IsStepCompleted(string flowId, string stepId)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId))
                return false;

            return Data.CompletedSteps.Contains(GetStepKey(flowId, stepId));
        }

        public string GetCurrentFlowId() => Data.CurrentFlowId;
        public string GetCurrentStepId() => Data.CurrentStepId;

        public void SetCurrentStep(string flowId, string stepId)
        {
            Data.CurrentFlowId = flowId;
            Data.CurrentStepId = stepId;
            TraceModify();
        }

        public void ClearCurrentStep()
        {
            Data.CurrentFlowId = null;
            Data.CurrentStepId = null;
            TraceModify();
        }

        public void MarkFlowCompleted(string flowId)
        {
            if (string.IsNullOrEmpty(flowId)) return;

            Data.CompletedFlows.Add(flowId);
            TraceModify();
        }

        public void MarkStepCompleted(string flowId, string stepId)
        {
            if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(stepId)) return;

            Data.CompletedSteps.Add(GetStepKey(flowId, stepId));
            TraceModify();
        }

        public void ClearProgress()
        {
            Data.CompletedFlows.Clear();
            Data.CompletedSteps.Clear();
            Data.CurrentFlowId = null;
            Data.CurrentStepId = null;
            TraceModify();
        }

        public void ImportFromSaveData(GuideProgressData saveData)
        {
            if (saveData == null) return;

            Data.CompletedFlows.Clear();
            Data.CompletedSteps.Clear();

            if (saveData.completedFlows != null)
            {
                foreach (var flowId in saveData.completedFlows)
                    Data.CompletedFlows.Add(flowId);
            }

            if (saveData.completedSteps != null)
            {
                foreach (var stepKey in saveData.completedSteps)
                    Data.CompletedSteps.Add(stepKey);
            }

            Data.CurrentFlowId = saveData.currentFlowId;
            Data.CurrentStepId = saveData.currentStepId;
            TraceModify();
        }

        public GuideProgressData ExportToSaveData()
        {
            return new GuideProgressData
            {
                completedFlows = Data.CompletedFlows.ToList(),
                completedSteps = Data.CompletedSteps.ToList(),
                currentFlowId = Data.CurrentFlowId,
                currentStepId = Data.CurrentStepId
            };
        }

        private static string GetStepKey(string flowId, string stepId) => $"{flowId}:{stepId}";
    }
}
