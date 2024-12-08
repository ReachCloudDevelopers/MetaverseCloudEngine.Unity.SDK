using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class KioskModeAPI : TriInspectorMonoBehaviour
    {
        public KioskModeOptions options = KioskModeOptions.MetaSpaceLocked;
        public UnityEvent onKioskModeTrue;
        public UnityEvent onKioskModeFalse;
        public UnityEvent<bool> onKioskMode;

        private void Awake()
        {
            OnKioskModeChanged(MetaverseKioskModeAPI.IsActive);
            MetaverseKioskModeAPI.ActivationChanged += OnKioskModeChanged;
        }

        private void OnDestroy()
        {
            MetaverseKioskModeAPI.ActivationChanged -= OnKioskModeChanged;
        }

        private void OnKioskModeChanged(bool value)
        {
            if (options != 0 && !MetaverseKioskModeAPI.Options.HasFlag(options))
                value = false; // If the options don't match, then we're not in kiosk mode.
            if (options.HasFlag(KioskModeOptions.OrganizationLocked))
            {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                if (MetaverseInternalResources.Instance.proxy)
                    value = true; // White label mode forces organization lock.
#endif
            }

            onKioskMode?.Invoke(value);
            if (value) onKioskModeTrue?.Invoke();
            else onKioskModeFalse?.Invoke();
        }
    }
}