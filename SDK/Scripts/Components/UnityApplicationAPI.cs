using MetaverseCloudEngine.Unity.Labels;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component that exposes <see cref="Application"/> specific functions.
    /// </summary>
    [HideMonoScript]
    public class UnityApplicationAPI : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// Quit the application but save it's state so that upon next startup the app will
        /// open in the original state.
        /// </summary>
        public void QuitAndSaveState()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            MetaverseDeepLinkAPI.SaveState();
#endif
            Quit();
        }

        /// <summary>
        /// Quit the application. NOTE: This will close the entire application, not just exit the meta space.
        /// </summary>
        public void Quit()
        {
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        /// <summary>
        /// Open an application URL.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        public void OpenURL(string url)
        {
            MVUtils.OpenURL(url);
        }

        /// <summary>
        /// Open an application URL from a label.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        public void OpenURL(LabelReference url)
        {
            url.label?.GetValueAsync(v =>
            {
                MVUtils.OpenURL(v);
            });
        }
    }
}
