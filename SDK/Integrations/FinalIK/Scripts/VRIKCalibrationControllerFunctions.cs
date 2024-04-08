#if METAVERSE_CLOUD_ENGINE
using MetaverseCloudEngine.Unity.Networking.Components;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.FinalIK
{
    [DisallowMultipleComponent]
    public partial class VRIKCalibrationControllerFunctions : NetworkObjectBehaviour
    {
        public UnityEvent onCalibrated;
        public UnityEvent onCalibrateFailed;

        public void Calibrate() => CalibrateInternal();
        partial void CalibrateInternal();
    }
}
#endif