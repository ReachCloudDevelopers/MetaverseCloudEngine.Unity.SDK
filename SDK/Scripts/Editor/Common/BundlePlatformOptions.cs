using UnityEditor;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class BundlePlatformOptions
    {
        public bool overrideDefaults;
        public BundleMaxTextureResolution maxTextureResolution = BundleMaxTextureResolution._4096;
        public bool compressTextures = true;
        public int compressorQuality = 100;
        public ModelImporterMeshCompression meshCompression = ModelImporterMeshCompression.Off;
    }
}