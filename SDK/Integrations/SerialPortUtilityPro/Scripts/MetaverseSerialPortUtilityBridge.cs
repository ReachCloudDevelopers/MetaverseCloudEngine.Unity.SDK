using System;
using System.Reflection;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

[assembly: Preserve]

namespace MetaverseCloudEngine.Unity.SPUP
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-int.MaxValue)]
    [HideMonoScript]
    public class MetaverseSerialPortUtilityBridge : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField]
        private Component spupComponent;

        [PropertySpace(10)]
        [SerializeField]
        private UnityEvent<string> onMessageReceived = new();
        [SerializeField]
        private UnityEvent onConnected = new();
        [SerializeField]
        private UnityEvent onDisconnected = new();

        private MethodInfo _writeMethod;
        private FieldInfo _readCompleteEventObjectField;
        private UnityAction<object> _delegateCall;

        private FieldInfo _onSystemEventField;
        private UnityAction<object, string> _onSystemEventCallback;

        public UnityEvent<string> OnMessageReceived => onMessageReceived;
        public UnityEvent OnConnected => onConnected;
        public UnityEvent OnDisconnected => onDisconnected;

        private void Reset() => MetaverseSerialPortUtilityInterop.EnsureComponent(ref spupComponent, gameObject);
        private void OnValidate() => MetaverseSerialPortUtilityInterop.EnsureComponent(ref spupComponent, gameObject);

        private void Awake()
        {
            if (!spupComponent) return;
            AddReadCompleteEventListener();
            MetaverseSerialPortUtilityInterop.AddSystemEventCallback(
                spupComponent,
                ref _onSystemEventField,
                OnSystemEventCallback,
                ref _onSystemEventCallback);
        }

        private void OnDestroy()
        {
            RemoveReadCompleteEventListener();
            MetaverseSerialPortUtilityInterop.RemoveSystemEventCallback(
                spupComponent,
                ref _onSystemEventField,
                OnSystemEventCallback);
        }

        public void Write(byte[] data)
        {
            try
            {
                MetaverseSerialPortUtilityInterop.CallInstanceMethod(
                    spupComponent,
                    ref _writeMethod,
                    MetaverseSerialPortUtilityInterop.InstanceMethodID.Write,
                    data);
            }
            catch (ArgumentOutOfRangeException)
            {
                // ignored
            }
        }

        private void AddReadCompleteEventListener()
        {
            if (_delegateCall != null) RemoveReadCompleteEventListener();
            try
            {
                var readCompleteEvent = MetaverseSerialPortUtilityInterop.GetField<UnityEventBase>(
                    spupComponent,
                    ref _readCompleteEventObjectField,
                    MetaverseSerialPortUtilityInterop.GettableFieldID.ReadCompleteEventObject);
                var addListener = readCompleteEvent.GetType()
                    .GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public);
                addListener?.Invoke(readCompleteEvent, new object[] { _delegateCall = OnStream });
            }
            catch (NullReferenceException)
            {
            }
        }

        private void RemoveReadCompleteEventListener()
        {
            if (_delegateCall == null) return;
            try
            {
                var readCompleteEvent = MetaverseSerialPortUtilityInterop.GetField<UnityEventBase>(
                    spupComponent,
                    ref _readCompleteEventObjectField,
                    MetaverseSerialPortUtilityInterop.GettableFieldID.ReadCompleteEventObject);
                var removeListener = readCompleteEvent.GetType()
                    .GetMethod("RemoveListener", BindingFlags.Instance | BindingFlags.Public);
                removeListener?.Invoke(readCompleteEvent, new object[] { _delegateCall });
            }
            catch (NullReferenceException)
            {
            }
        }

        private void OnStream(object message) => onMessageReceived?.Invoke(message?.ToString());

        private void OnSystemEventCallback(object sender, string e)
        {
            switch (e?.ToUpperInvariant())
            {
                case "OPENED":
                    onConnected?.Invoke();
                    break;
                case "CLOSED":
                case "OPEN_ERROR":
                case "BT_DISCONNECT_TO_SERVERMODE":
                case "LICENSE_ERROR":
                    onDisconnected?.Invoke();
                    break;
            }
        }
    }
}