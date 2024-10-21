using UnityEngine;

#if MV_XR_TOOLKIT_3
using XRSocketInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
#else
using XRSocketInteractor = UnityEngine.XR.Interaction.Toolkit.XRSocketInteractor;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [RequireComponent(typeof(XRSocketInteractor))]
    public class MetaverseSocketIdentifier : MonoBehaviour
    {
        public int socketType;
    }
}