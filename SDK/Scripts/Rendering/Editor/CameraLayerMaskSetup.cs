using MetaverseCloudEngine.Unity.Assets.MetaSpaces.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    /// Ensures that the camera layer mask is set to the correct value (exclude layer 3).
    /// </summary>
    public class CameraLayerMaskProcessor : IMetaSpaceSceneSetup
    {
        public bool SetupMainCamera(Camera camera)
        {
            if ((camera.cullingMask & (1 << 3)) == 0)
                return false;
            camera.cullingMask &= ~(1 << 3);
            return true;
        }
    }
}
