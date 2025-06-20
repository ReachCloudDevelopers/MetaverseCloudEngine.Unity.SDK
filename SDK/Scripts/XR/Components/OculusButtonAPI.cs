using System;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [HideMonoScript]
    public class OculusButtonAPI : TriInspectorMonoBehaviour
    {
        [Flags]
        public enum Button
        {
            None                      = 0,          
            One                       = 0x00000001, 
            Two                       = 0x00000002, 
            Three                     = 0x00000004, 
            Four                      = 0x00000008, 
            Start                     = 0x00000100, 
            Back                      = 0x00000200, 
            PrimaryShoulder           = 0x00001000, 
            PrimaryIndexTrigger       = 0x00002000, 
            PrimaryHandTrigger        = 0x00004000, 
            PrimaryThumbstick         = 0x00008000, 
            PrimaryThumbstickUp       = 0x00010000, 
            PrimaryThumbstickDown     = 0x00020000, 
            PrimaryThumbstickLeft     = 0x00040000, 
            PrimaryThumbstickRight    = 0x00080000, 
            PrimaryTouchpad           = 0x00000400, 
            SecondaryShoulder         = 0x00100000, 
            SecondaryIndexTrigger     = 0x00200000, 
            SecondaryHandTrigger      = 0x00400000, 
            SecondaryThumbstick       = 0x00800000, 
            SecondaryThumbstickUp     = 0x01000000, 
            SecondaryThumbstickDown   = 0x02000000, 
            SecondaryThumbstickLeft   = 0x04000000, 
            SecondaryThumbstickRight  = 0x08000000, 
            SecondaryTouchpad         = 0x00000800, 
            DpadUp                    = 0x00000010, 
            DpadDown                  = 0x00000020, 
            DpadLeft                  = 0x00000040, 
            DpadRight                 = 0x00000080, 
            Up                        = 0x10000000, 
            Down                      = 0x20000000, 
            Left                      = 0x40000000, 
            Right      = unchecked((int)0x80000000),
            Any                       = ~None,      
        }

        public Button button = Button.None;
        public UnityEvent onButtonDown = new();
        public UnityEvent onButtonUp = new();

#if MV_META_CORE
        private void Update()
        {
            if (OVRInput.GetDown((OVRInput.Button)button))
                onButtonDown?.Invoke();
            if (OVRInput.GetUp((OVRInput.Button)button))
                onButtonUp?.Invoke();
        }
#endif
    }
}