using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class DebugLog : TriInspectorMonoBehaviour
    {
        public enum LogLevel
        {
            Debug,
            Warning,
            Error
        }
        
        public LogLevel logLevel = LogLevel.Debug;
        public string format = "{0}";
        
        public void Log(string o)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                    MetaverseProgram.Logger.Log(string.Format(format, o));
                    break;
                case LogLevel.Warning:
                    MetaverseProgram.Logger.LogWarning(string.Format(format, o));
                    break;
                case LogLevel.Error:
                    MetaverseProgram.Logger.LogError(string.Format(format, o));
                    break;
            }
        }
    }
}