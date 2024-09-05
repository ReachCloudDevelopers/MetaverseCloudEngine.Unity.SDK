using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SPUP
{
    public class MetaverseSerialPortDeviceListUi : TriInspectorMonoBehaviour
    {
        [Required] [SerializeField] private Component serialPortUtilityPro;
        [SerializeField] private bool refreshListOnEnable = true;
        [Required] [SerializeField] private MetaverseSerialPortDeviceListAPI listApi;
        [Required] [SerializeField] private MetaverseSerialPortDeviceListUiItem itemPrefab;
        [Required] [SerializeField] private RectTransform contentRect;
        
        private readonly List<MetaverseSerialPortDeviceListUiItem> _items = new();
        private static bool _ignoreEvents;

        private void Awake()
        {
            listApi.onAnyDeviceFound.AddListener(OnAnyDevices);
            listApi.onNoDevicesFound.AddListener(OnNoDevices);
            listApi.onDeviceFound.AddListener(OnDeviceFound);
            listApi.onSerialPortOpened.AddListener(OnSerialPortOpened);
            listApi.onSerialPortClosed.AddListener(OnSerialPortClosed);

            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void OnValidate()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void Reset()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref serialPortUtilityPro, gameObject);
        }

        private void OnEnable()
        {
            if (refreshListOnEnable)
                listApi.ListDevices();
        }

        private void OnDestroy()
        {
            listApi.onAnyDeviceFound.RemoveListener(OnAnyDevices);
            listApi.onNoDevicesFound.RemoveListener(OnNoDevices);
            listApi.onDeviceFound.RemoveListener(OnDeviceFound);
            listApi.onSerialPortOpened.RemoveListener(OnSerialPortOpened);
            listApi.onSerialPortClosed.RemoveListener(OnSerialPortClosed);
            
            ClearAllDeviceUis();
        }

        private void OnAnyDevices()
        {
            ClearAllDeviceUis();
        }

        private void OnNoDevices()
        {
            ClearAllDeviceUis();
        }

        private void OnDeviceFound(string device, MetaverseSerialPortUtilityInterop.DeviceInfo data, MetaverseSerialPortUtilityInterop.OpenSystem openSystem)
        {
            var item = Instantiate(itemPrefab, contentRect);
            item.Repaint(serialPortUtilityPro, device, data, openSystem);

            item.onDeviceOpen.AddListener(OnDeviceOpened);
            item.onDeviceClosed.AddListener(OnDeviceClosed);
            _items.Add(item);
            return;

            void OnDeviceOpened()
            {
                if (_ignoreEvents)
                    return;
                _ignoreEvents = true;
                try
                {
                    foreach (var x in _items.Where(y => y != item))
                        x.RepaintOpenedState();
                }
                finally
                {
                    _ignoreEvents = false;
                }
            }

            void OnDeviceClosed()
            {
                if (_ignoreEvents)
                    return;
                _ignoreEvents = true;
                try
                {
                    foreach (var x in _items.Where(y => y != item))
                        x.RepaintOpenedState();
                }
                finally
                {
                    _ignoreEvents = false;
                }
            }
        }

        private void ClearAllDeviceUis()
        {
            foreach (var item in _items.Where(item => item)) Destroy(item.gameObject);
        }

        private void OnSerialPortOpened()
        {
            if (_ignoreEvents)
                return;
            foreach (var item in _items)
                item.RepaintOpenedState();
        }

        private void OnSerialPortClosed()
        {
            if (_ignoreEvents)
                return;
            foreach (var item in _items)
                item.RepaintOpenedState();
        }
    }
}