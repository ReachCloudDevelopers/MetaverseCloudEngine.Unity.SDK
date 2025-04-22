using UnityEngine;

namespace MetaverseCloudEngine.Unity.SPUP
{
    [Preserve]
    [AddComponentMenu("")]
    public class SPUPLinker : MonoBehaviour
    {
        [Preserve]
        private void Start()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL || SERIAL_PORT_UTILITY_PRO
            var spup = GetComponent<SerialPortUtility.SerialPortUtilityPro>();
            spup.SystemEventObject.AddListener((s, s1) =>
            {
                Debug.Log($"SPUP Event: {s} {s1}");
            });
            spup.ReadEventObject.RemoveListener((o) =>
            {
                Debug.Log($"SPUP Read: {o}");
            });
            spup.ReadCompleteEventObject.AddListener((o) =>
            {
                Debug.LogError($"SPUP Error: {o}");
            });
            spup.ReadCompleteEventObject.RemoveListener((o) =>
            {
                Debug.Log($"SPUP Read: {o}");
            });
#endif
        }
    }
}