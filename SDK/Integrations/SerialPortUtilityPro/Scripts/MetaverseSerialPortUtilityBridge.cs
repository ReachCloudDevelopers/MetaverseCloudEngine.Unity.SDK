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
    public class MetaverseSerialPortUtilityBridge : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField]
        private Component spupComponent;
        
        [Title("Events")]
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
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(spupComponent, ref _writeMethod, "Write", data);
        }

        private void AddListener()
        {
            var readCompleteEvent = MetaverseSerialPortUtilityInterop.GetField<UnityEventBase>(spupComponent, ref _readCompleteEventObjectField, "ReadCompleteEventObject");
            var addListenerCallFunction = readCompleteEvent.GetType().GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public)!;
            addListenerCallFunction.Invoke(readCompleteEvent, new object[] { _delegateCall = OnMessageReceived });
        }

        private void RemoveListener()
        {
            var readCompleteEvent = MetaverseSerialPortUtilityInterop.GetField<UnityEventBase>(spupComponent, ref _readCompleteEventObjectField, "ReadCompleteEventObject");
            var removeListenerCallFunction = readCompleteEvent.GetType().GetMethod("RemoveListener", BindingFlags.Instance | BindingFlags.Public)!;
            removeListenerCallFunction.Invoke(readCompleteEvent, new object[] { _delegateCall });
        }

        private void OnMessageReceived(object message)
        {
            
        }
    }
}
