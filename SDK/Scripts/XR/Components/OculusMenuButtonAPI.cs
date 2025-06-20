using System;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class OculusMenuButtonAPI : TriInspectorMonoBehaviour
    {
        public UnityEvent onMenuButtonPressed;

        private void Awake()
        {
        }

#if UNITY_ANDROID && MV_OCULUS_PLUGIN
        
#endif
    }
}