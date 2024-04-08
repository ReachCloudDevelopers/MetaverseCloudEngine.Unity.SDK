#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.CloudData.Components;
using System;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class SetCloudDataRecord : FsmStateAction
    {
        [RequiredField] public FsmString templateID;
        [RequiredField] public FsmString key;

        [Title("Blockchain Options (Show/Hide)")] public bool expandBlockchainSettings;
        [Title("Land Plot Options (Show/Hide)")] public bool expandLandPlotSettings;
        [Title("Inputs (Show/Hide)")] public bool expandInputs;
        [Title("Events (Show/Hide)")] public bool expandEvents;

        [HideIf(nameof(HideBlockchainOptions))][Title("")][ActionSection("- Blockchain -")] public FsmBlockchainCloudDataRecordParams blockchain;
        [HideIf(nameof(HideLandPlotOptions))][Title("")][ActionSection("- Land Plot -")] public FsmLandPlotCloudDataRecordParams landPlotOptions;
        [HideIf(nameof(HideInputs))][Title("")][ActionSection("- Inputs -")] public FsmCloudDataRecordInputs inputs;
        [HideIf(nameof(HideEvents))][Title("")][ActionSection("- Events -")] public FsmCloudDataRecordEvents events;

        public override string ErrorCheck()
        {
            if (!Guid.TryParse(templateID.Value, out var templateGuid))
                return "Template ID is not a valid GUID";
            return base.ErrorCheck();
        }

        public override void OnEnter()
        {
            var cdr = Fsm.GameObject.AddComponent<CloudDataRecord>();
            cdr.Key = key.Value;
            cdr.RecordTemplateIdString = templateID.Value;
            
            if (!inputs.setStringValue.IsNone) cdr.StringValue = inputs.setStringValue.Value;
            if (!inputs.setNumberValue.IsNone) cdr.FloatValue = inputs.setNumberValue.Value;
            if (!inputs.setBoolValue.IsNone) cdr.BoolValue = inputs.setBoolValue.Value;

            cdr.BlockchainSource = blockchain.blockchainSource.Value;
            cdr.BlockchainType = (int)(BlockchainType)blockchain.blockchainType.Value;
            cdr.LandOptions.appendBuildableIDToKey = landPlotOptions.appendBuildableIDToKey.Value;
            cdr.LandOptions.deleteOnBuildableRemoved = landPlotOptions.deleteOnBuildableRemoved.Value;
            cdr.Events.onSaveSuccess.AddListener(() =>
            {
                events.storeSucceeded.Value = true;
                Fsm.Event(events.onSuccess);
            });
            cdr.Events.onSaveFailed.AddListener(() =>
            {
                events.storeSucceeded.Value = false;
                Fsm.Event(events.onFailed);
            });
            cdr.Events.onSaveFinished.AddListener(() =>
            {
                Fsm.Event(events.onFinished);
                UnityEngine.Object.Destroy(cdr);
                Finish();
            });

            cdr.LoadOnStart = false;

            cdr.Save();
        }

        public bool HideInputs() => !expandInputs;
        public bool HideEvents() => !expandEvents;
        public bool HideBlockchainOptions() => !expandBlockchainSettings;
        public bool HideLandPlotOptions() => !expandLandPlotSettings;
    }
}
#endif