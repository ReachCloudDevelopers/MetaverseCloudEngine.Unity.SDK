#if UNITY_IOS && !UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MetaverseCloudEngine.Unity.Editors
{
    internal static class IgnoreUnalignedPointersInLinker
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target == BuildTarget.iOS)
            {
                UnityEngine.Debug.Log("Adding linker flag to ignore unaligned pointers");
                
                // Get the Xcode project path
                string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

                // Initialize the project object
                PBXProject proj = new PBXProject();
                proj.ReadFromFile(projPath);

                // Get the target GUID depending on the Unity version
                string targetGUID = GetTargetGUID(proj);

                // Add the linker flag to the target's build settings
                proj.AddBuildProperty(targetGUID, "OTHER_LDFLAGS", "-Wl,-no_fixup_chains");

                // Write the updated project back to the file
                proj.WriteToFile(projPath);
            }
        }
        
        private static string GetTargetGUID(PBXProject proj)
        {
#if UNITY_2019_3_OR_NEWER
            // For Unity 2019.3 and newer
            return proj.GetUnityMainTargetGuid();
#else
            // For Unity versions prior to 2019.3
            return proj.TargetGuidByName("Unity-iPhone");
#endif
        }
    }
}
#endif
