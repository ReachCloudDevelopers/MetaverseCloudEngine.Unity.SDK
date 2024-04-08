using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    /// <summary>
    /// A helper component that allows you to limit the frame rate of the application.
    /// </summary>
    [HideMonoScript]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Rendering/Frame Rate Limiter")]
    public class FrameRateLimiter : TriInspectorMonoBehaviour
    {
        [Tooltip("The maximum frame rate to limit the application to.")]
        [Min(1)]
        [SerializeField] private int targetFrameRate = 120;

        /// <summary>
        /// Gets or sets the target frame rate.
        /// </summary>
        public int TargetFrameRate { get { return targetFrameRate; } set { targetFrameRate = value; } }

        private void Update()
        {
            Application.targetFrameRate = targetFrameRate;
        }
    }
}
