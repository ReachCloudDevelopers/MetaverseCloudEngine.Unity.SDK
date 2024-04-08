namespace MetaverseCloudEngine.Unity.Encryption
{
    public interface IEncryptor
    {
        string EncryptString(string value);
        string DecryptString(string value);
    }
}