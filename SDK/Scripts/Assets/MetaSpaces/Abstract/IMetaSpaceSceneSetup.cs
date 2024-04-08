using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces.Abstract
{
    /// <summary>
    /// Interface for setting up the main camera for a meta space.
    /// </summary>
    public interface IMetaSpaceSceneSetup
    {
        /// <summary>
        /// Called when the main camera should be set up.
        /// </summary>
        /// <param name="camera">The main camera's camera component.</param>
        /// <returns>A value indicating whether the camera was successfully set up.</returns>
        bool SetupMainCamera(Camera camera);
    }
}