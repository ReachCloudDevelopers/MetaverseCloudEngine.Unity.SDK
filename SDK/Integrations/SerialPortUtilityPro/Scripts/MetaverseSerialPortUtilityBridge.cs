using System;
using System.Linq;
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
    public class MetaverseSerialPortUtilityBridge : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField]
        private Component spupComponent;
        
        [Title("Read Data")]
        [SerializeField] private UnityEvent<string> onMessageReceived;
        
        private MethodInfo _writeMethod;
        private FieldInfo _readCompleteEventObjectField;
        private UnityAction<object> _delegateCall;

        private void Reset()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref spupComponent, gameObject);
        }

        private void OnValidate()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref spupComponent, gameObject);
        }

        private void Awake()
        {
            if (!spupComponent)
                return;

            if (onMessageReceived.GetPersistentEventCount() > 0)
            {
                AddListener();
            }
        }

        private void OnDestroy()
        {
            RemoveListener();
        }

        public void Write(byte[] data)
        {
            try
            {
                MetaverseSerialPortUtilityInterop.CallInstanceMethod(spupComponent, ref _writeMethod, MetaverseSerialPortUtilityInterop.InstanceMethodID.Write, data);
            }
            catch (ArgumentOutOfRangeException)
            {
                // ignored
            }
        }

        private void AddListener()
        {
            if (_delegateCall is not null)
                RemoveListener();
            
            var readCompleteEvent = MetaverseSerialPortUtilityInterop.GetField<UnityEventBase>(spupComponent, ref _readCompleteEventObjectField, MetaverseSerialPortUtilityInterop.GettableFieldID.ReadCompleteEventObject);
            var addListenerCallFunction = readCompleteEvent.GetType().GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public)!;
            addListenerCallFunction.Invoke(readCompleteEvent, new object[] { _delegateCall = OnStream });
        }

        private void RemoveListener()
        {
            if (_delegateCall is null)
                return;
            
            var readCompleteEvent = MetaverseSerialPortUtilityInterop.GetField<UnityEventBase>(spupComponent, ref _readCompleteEventObjectField, MetaverseSerialPortUtilityInterop.GettableFieldID.ReadCompleteEventObject);
            var removeListenerCallFunction = readCompleteEvent.GetType().GetMethod("RemoveListener", BindingFlags.Instance | BindingFlags.Public)!;
            removeListenerCallFunction?.Invoke(readCompleteEvent, new object[] { _delegateCall });
        }

        private void OnStream(object message)
        {
            onMessageReceived?.Invoke(message.ToString());
        }
    }
}
