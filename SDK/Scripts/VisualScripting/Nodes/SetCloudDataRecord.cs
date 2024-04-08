using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.CloudData.Components;

using System.Collections;

using Unity.VisualScripting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.VisualScripting.Nodes
{
    [UnitCategory(MetaverseConstants.ProductName + "/Cloud Data")]
    public class SetCloudDataRecord : Unit
    {
        [PortLabelHidden]
        [DoNotSerialize] public ControlInput input;
        [DoNotSerialize] public ControlOutput onSuccess;
        [DoNotSerialize] public ControlOutput onFailed;
        [DoNotSerialize] public ControlOutput onFinished;
        [DoNotSerialize] public ValueOutput succeeded;

        [DoNotSerialize] public ValueInput templateID;
        [DoNotSerialize] public ValueInput key;

        [NullMeansSelf, DoNotSerialize] public ValueInput sourceObject;

        [DoNotSerialize] public ValueInput blockchainSource;
        [DoNotSerialize] public ValueInput blockchainType;

        [DoNotSerialize] public ValueInput appendBuildableIDToKey;
        [DoNotSerialize] public ValueInput deleteOnBuildableRemoved;

        [DoNotSerialize] public ValueInput stringValue;
        [DoNotSerialize] public ValueInput numberValue;
        [DoNotSerialize] public ValueInput boolValue;

        protected override void Definition()
        {
            input = ControlInputCoroutine(nameof(input), (flow) =>
            {
                return InputFlowEnumerator(flow);
            });

            templateID = ValueInput(nameof(templateID), "");
            key = ValueInput(nameof(key), "");
            sourceObject = ValueInput<GameObject>(nameof(sourceObject), null);

            blockchainSource = ValueInput(nameof(blockchainSource), "");
            blockchainType = ValueInput(nameof(blockchainType), BlockchainType.None);

            appendBuildableIDToKey = ValueInput(nameof(appendBuildableIDToKey), true);
            deleteOnBuildableRemoved = ValueInput(nameof(deleteOnBuildableRemoved), true);

            onSuccess = ControlOutput(nameof(onSuccess));
            onFailed = ControlOutput(nameof(onFailed));
            onFinished = ControlOutput(nameof(onFinished));
            succeeded = ValueOutput<bool>(nameof(succeeded));

            stringValue = ValueInput<string>(nameof(stringValue), null);
            numberValue = ValueInput<float>(nameof(numberValue), 0);
            boolValue = ValueInput(nameof(boolValue), false);
        }

        private IEnumerator InputFlowEnumerator(Flow flow)
        {
            var isFinished = false;

            var sourceObjVal = flow.GetValue<GameObject>(sourceObject);
            var cdr = (sourceObjVal ? sourceObjVal : flow.stack.component.gameObject).AddComponent<CloudDataRecord>();

            cdr.StringValue = flow.GetValue<string>(stringValue);
            cdr.BoolValue = flow.GetValue<bool>(boolValue);
            cdr.FloatValue = flow.GetValue<float>(numberValue);

            cdr.Key = flow.GetValue<string>(key);
            cdr.RecordTemplateIdString = flow.GetValue<string>(templateID);
            cdr.BlockchainSource = flow.GetValue<string>(blockchainSource);
            cdr.BlockchainType = (int)flow.GetValue<BlockchainType>(blockchainType);
            cdr.LandOptions.appendBuildableIDToKey = flow.GetValue<bool>(appendBuildableIDToKey);
            cdr.LandOptions.deleteOnBuildableRemoved = flow.GetValue<bool>(deleteOnBuildableRemoved);
            cdr.Events.onSaveSuccess.AddListener(() =>
            {
                flow.SetValue(succeeded, true);
                flow.Invoke(onSuccess);
            });
            cdr.Events.onSaveFailed.AddListener(() =>
            {
                flow.SetValue(succeeded, false);
                flow.Invoke(onFailed);
            });
            cdr.Events.onSaveFinished.AddListener(() =>
            {
                flow.Invoke(onFinished);
                isFinished = true;
            });
            
            cdr.LoadOnStart = false;

            cdr.Save();

            yield return new WaitUntil(() => isFinished);
        }
    }
}
