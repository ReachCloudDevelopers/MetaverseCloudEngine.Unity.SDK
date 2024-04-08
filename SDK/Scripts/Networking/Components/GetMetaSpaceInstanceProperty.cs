using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [Experimental]
    [HideMonoScript]
    public partial class GetMetaSpaceInstanceProperty : MetaSpaceBehaviour
    {
        [SerializeField] private string propertyName;
        [SerializeField] private string defaultValue;

        [Header("Value Callbacks")]
        public UnityEvent<int> onIntValue;
        public UnityEvent<float> onFloatValue;
        public UnityEvent<bool> onBoolValue;
        public UnityEvent<string> onStringValue;
        public UnityEvent onDefaultValue;

        private IMetaSpaceNetworkingService _networking;

        public string PropertyName {
            get => propertyName;
            set => propertyName = value;
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;

            base.OnDestroy();

            if (_networking != null)
                _networking.Ready -= OnNetworkingReady;
        }

        public void GetValue()
        {
            string value = null;
            GetValueInternal(ref value);

            if (string.IsNullOrEmpty(value))
            {
                value = defaultValue;
                onDefaultValue?.Invoke();
                if (string.IsNullOrEmpty(value))
                    return;
            }

            value = value.Trim();

            if (onIntValue.GetPersistentEventCount() > 0 && int.TryParse(value, out int i))
                onIntValue.Invoke(i);
            if (onFloatValue.GetPersistentEventCount() > 0 && float.TryParse(value, out float f))
                onFloatValue.Invoke(f);
            if (onBoolValue.GetPersistentEventCount() > 0 && bool.TryParse(value, out bool b))
                onBoolValue.Invoke(b);
            if (onStringValue.GetPersistentEventCount() > 0)
                onStringValue.Invoke(value);
        }

        protected override void OnMetaSpaceServicesRegistered()
        {
            _networking = MetaSpace.GetService<IMetaSpaceNetworkingService>();
            if (_networking.IsReady)
                OnNetworkingReady();
            else _networking.Ready += OnNetworkingReady;
        }

        private void OnNetworkingReady() => GetValue();

        partial void GetValueInternal(ref string value);
    }
}
