using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    [Experimental]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class EnableInputActions : TriInspectorMonoBehaviour
    {
        public InputActionReference[] actions;
        public InputActionAsset[] assets;

        private void Awake()
        {
            if (actions != null)
                foreach (var action in actions)
                    if (action && action.action != null)
                        action.action.Enable();
            if (assets != null)
                foreach (var asset in assets)
                    if (asset) asset.Enable();
        }
    }
}
