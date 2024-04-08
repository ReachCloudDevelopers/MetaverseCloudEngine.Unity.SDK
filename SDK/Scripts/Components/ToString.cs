using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class ToString : TriInspectorMonoBehaviour
    {
        public string format = "{0}";
        public UnityEvent<string> onConverted;

        public void Convert(float f)
        {
            onConverted?.Invoke(string.Format(format, f));
        }

        public void Convert(string s)
        {
            onConverted?.Invoke(string.Format(format, s));
        }

        public void Convert(int i)
        {
            onConverted?.Invoke(string.Format(format, i));
        }
        
        public void Convert(UnityEngine.Object o)
        {
            onConverted?.Invoke(string.Format(format, o));
        }
    }
}