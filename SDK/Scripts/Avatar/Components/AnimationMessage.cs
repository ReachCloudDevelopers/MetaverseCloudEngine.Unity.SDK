using System;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [Serializable]
    public class AnimationMessage
    {
        [Required]
        public AnimationMessageHandle handle;

        public UnityEvent onReceived;
        public UnityEvent<int> onIntValue;
        public UnityEvent<float> onFloatValue;
        public UnityEvent<bool> onBoolValue;
        public UnityEvent<string> onStringValue;
        public UnityEvent<UnityEngine.Object> onObjectValue;
        public UnityEvent onObjectValueNull;

        public void OnReceived(AnimationMessageHandle h)
        {
            onReceived?.Invoke();
            onIntValue?.Invoke(h.intValue);
            onFloatValue?.Invoke(h.floatValue);
            onBoolValue?.Invoke(h.boolValue);
            onStringValue?.Invoke(h.stringValue);
            if (h.objectValue != null) onObjectValue?.Invoke(h.objectValue);
            else onObjectValueNull?.Invoke();
        }
    }
}