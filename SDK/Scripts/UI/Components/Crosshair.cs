using MetaverseCloudEngine.Unity.Inputs.Components;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [HideMonoScript]
    public class Crosshair : TriInspectorMonoBehaviour
    {
        private void Awake()
        {
            PlayerInputAPI.CrosshairEnabledChanged += OnCrosshairEnabledChanged;
        }

        private void OnDestroy()
        {
            PlayerInputAPI.CrosshairEnabledChanged -= OnCrosshairEnabledChanged;
        }

        private void OnCrosshairEnabledChanged()
        {
            gameObject.SetActive(PlayerInputAPI.CrosshairEnabled);
        }
    }
}