namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    public interface IPrefs
    {
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        string GetString(string key, string defaultValue);
        void SetString(string key, string value);
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        void DeleteKey(string key);
    }
}
