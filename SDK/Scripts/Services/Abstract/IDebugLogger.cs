namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    public interface IDebugLogger
    {
        void Log(object content);
        void LogWarning(object content);
        void LogError(object content);
    }
}
