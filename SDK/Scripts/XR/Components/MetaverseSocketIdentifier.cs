using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [RequireComponent(typeof(XRSocketInteractor))]
    public class MetaverseSocketIdentifier : MonoBehaviour
    {
        public int socketType;
    }
}