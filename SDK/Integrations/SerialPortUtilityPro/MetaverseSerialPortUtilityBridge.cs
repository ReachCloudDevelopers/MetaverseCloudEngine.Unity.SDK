using System.Linq;
using System.Reflection;
using TriInspectorMVCE;
using UnityEngine;
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
        
        private MethodInfo _writeMethod;

        private void Reset()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref spupComponent, gameObject);
        }

        private void OnValidate()
        {
            MetaverseSerialPortUtilityInterop.EnsureComponent(ref spupComponent, gameObject);
        }

        public void Write(byte[] data)
        {
            MetaverseSerialPortUtilityInterop.CallInstanceMethod(spupComponent, ref _writeMethod, "Write", data);
        }
    }
}
