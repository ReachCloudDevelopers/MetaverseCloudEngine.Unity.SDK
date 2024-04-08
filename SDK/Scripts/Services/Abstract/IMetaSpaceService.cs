namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    /// <summary>
    /// A service that is responsible for managing a particular aspect of the meta-space.
    /// </summary>
    public interface IMetaSpaceService
    {
        /// <summary>
        /// Initializes the service.
        /// </summary>
        void Initialize();
        /// <summary>
        /// Disposes of the service.
        /// </summary>
        void Dispose();
    }
}