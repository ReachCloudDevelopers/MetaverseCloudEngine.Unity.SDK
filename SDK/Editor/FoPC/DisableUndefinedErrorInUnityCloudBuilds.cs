#if (UNITY_IOS && UNITY_EDITOR) && UNITY_CLOUD_BUILD
using System;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS;

namespace MetaverseCloudEngine.Unity.Editors.FoPC
{
    public class DisableUndefinedErrorInUnityCloudBuilds
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuildProject)
        {
            if (buildTarget == BuildTarget.iOS)
            {
                Debug.Log("Adjusting LDFLAGS to ignore undefined symbols during build.");
              
                string pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
                PBXProject pbxProject = new PBXProject();
                pbxProject.ReadFromFile(pbxProjectPath);
    
    #if UNITY_2019_3_OR_NEWER
                string targetGuid = pbxProject.GetUnityFrameworkTargetGuid();
    #else
                string targetGuid = pbxProject.GetUnityMainTargetGuid();
    #endif
                pbxProject.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-Wl,-undefined,dynamic_lookup,-no_fixup_chains");
                pbxProject.WriteToFile(pbxProjectPath);
            }
        }
    }
}
#endif
