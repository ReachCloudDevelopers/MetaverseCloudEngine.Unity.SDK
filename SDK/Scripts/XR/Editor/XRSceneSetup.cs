#if UNITY_EDITOR

using UnityEngine;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces.Abstract;

namespace MetaverseCloudEngine.Unity.XR.Editor
{
    public class XRSceneSetup : IMetaSpaceSceneSetup
    {
        public bool SetupMainCamera(Camera camera)
        {
            return false; // Deprecated.
        }
    }
}

#endif