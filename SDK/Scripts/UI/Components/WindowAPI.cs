using System.Linq;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    /// <summary>
    /// A component that allows the SDK to interact with core application
    /// windows such as the login window, the settings window, and the
    /// remote connection window, etc.
    /// </summary>
    [HideMonoScript]
    public class WindowAPI : TriInspectorMonoBehaviour
    {
        public void Open(string windowID)
        {
            var window = MVUtils.FindObjectsOfTypeNonPrefabPooled<Window>(true);
            foreach (var w in window)
            {
                if (w.ID == windowID)
                    w.Open();
            }
        }
        
        public void Close(string windowID)
        {
            var window = MVUtils.FindObjectsOfTypeNonPrefabPooled<Window>(true);
            foreach (var w in window)
            {
                if (w.ID == windowID)
                    w.Close();
            }
        }
    }
}