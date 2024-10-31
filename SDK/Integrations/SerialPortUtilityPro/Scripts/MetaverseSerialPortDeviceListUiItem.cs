using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SPUP
{
    [HideMonoScript]
    public class MetaverseSerialPortDeviceListUiItem : MonoBehaviour
    {
        public UnityEvent<string> onDeviceName = new();
        public UnityEvent onStartedOpening = new();
        public UnityEvent onStoppedOpening = new();
        public UnityEvent onDeviceOpen = new();
        public UnityEvent onDeviceClosed = new();
        
        private readonly MetaverseSerialPortDeviceAPI _deviceAPI = new();

        public void Repaint(
            Component spu,
            string device, 
            MetaverseSerialPortUtilityInterop.DeviceInfo data, 
            MetaverseSerialPortUtilityInterop.OpenSystem openSystem)
        {
            _deviceAPI.Initialize(spu, device, data, openSystem);
            _deviceAPI.OnDeviceName.AddListener(onDeviceName.Invoke);
            _deviceAPI.OnStartedOpening.AddListener(onStartedOpening.Invoke);
            _deviceAPI.OnStoppedOpening.AddListener(onStoppedOpening.Invoke);
            _deviceAPI.OnDeviceOpen.AddListener(onDeviceOpen.Invoke);
            _deviceAPI.OnDeviceClosed.AddListener(onDeviceClosed.Invoke);
        }

        public void RepaintOpenedState(bool closed = false) => _deviceAPI.CheckOpened(closed);

        public void Open() => _deviceAPI.Open();
    }
}