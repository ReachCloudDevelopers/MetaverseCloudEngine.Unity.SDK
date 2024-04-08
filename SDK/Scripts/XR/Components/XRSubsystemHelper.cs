using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// A component which provides functions that allow you to 
    /// start / stop the primary XR subsystem.
    /// </summary>
    public class XRSubsystemHelper : MonoBehaviour
    {
        public void StartXR() => XRSubsystemAPI.StartXR();
        public void StopXR() => XRSubsystemAPI.StopXR();
    }
}