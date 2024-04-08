using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.CloudData.Components;

using System.Collections;

using Unity.VisualScripting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.VisualScripting.Nodes
{
    [UnitCategory(MetaverseConstants.ProductName + "/Cloud Data")]
    public class GetCloudDataRecord : Unit
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

        [DoNotSerialize] public ValueOutput stringValue;
        [DoNotSerialize] public ValueOutput intValue;
        [DoNotSerialize] public ValueOutput floatValue;
        [DoNotSerialize] public ValueOutput boolValue;

        protected override void Definition()
        {
            input = ControlInputCoroutine(nameof(input), flow =>
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

            stringValue = ValueOutput<string>(nameof(stringValue));
            intValue = ValueOutput<int>(nameof(intValue));
            floatValue = ValueOutput<float>(nameof(floatValue));
            boolValue = ValueOutput<bool>(nameof(boolValue));

            Succession(input, onSuccess);
            Succession(input, onFinished);
            Succession(input, onFailed);
        }

        private IEnumerator InputFlowEnumerator(Flow flow)
        {
            bool isFinished = false;

            GameObject sourceObjVal = flow.GetValue<GameObject>(sourceObject);
            CloudDataRecord cdr = (sourceObjVal ? sourceObjVal : flow.stack.component.gameObject).AddComponent<CloudDataRecord>();
            cdr.Key = flow.GetValue<string>(key);
            cdr.RecordTemplateIdString = flow.GetValue<string>(templateID);
            cdr.BlockchainSource = flow.GetValue<string>(blockchainSource);
            cdr.BlockchainType = (int)flow.GetValue<BlockchainType>(blockchainType);
            cdr.LandOptions.appendBuildableIDToKey = flow.GetValue<bool>(appendBuildableIDToKey);
            cdr.LandOptions.deleteOnBuildableRemoved = flow.GetValue<bool>(deleteOnBuildableRemoved);
            cdr.Events.onLoadSuccess.AddListener(() =>
            {
                flow.SetValue(stringValue, cdr.StringValue);
                flow.SetValue(boolValue, cdr.BoolValue);
                flow.SetValue(floatValue, cdr.FloatValue);
                flow.SetValue(intValue, cdr.IntValue);
                flow.SetValue(succeeded, true);
                flow.Invoke(onSuccess);
            });
            cdr.Events.onLoadFailed.AddListener(() =>
            {
                flow.SetValue(succeeded, false);
                flow.Invoke(onFailed);
            });
            cdr.Events.onLoadFinished.AddListener(() =>
            {
                flow.Invoke(onFinished);
                isFinished = true;
            });

            cdr.LoadOnStart = true;

            yield return new WaitUntil(() => isFinished);
        }
    }
}
