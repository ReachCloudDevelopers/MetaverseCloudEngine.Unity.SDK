#if UNITY_IOS
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.IO;

public class AddCoreBluetoothIOS
{
    [UnityEditor.Callbacks.PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.iOS)
        {
            // Path to the Xcode project.
            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Get the target GUID. For newer Unity versions, GetUnityMainTargetGuid() is recommended.
            string targetGUID = proj.GetUnityMainTargetGuid();

            // Add the CoreBluetooth framework.
            proj.AddFrameworkToProject(targetGUID, "CoreBluetooth.framework", false);

            // Optionally add other frameworks here if needed.
            // proj.AddFrameworkToProject(targetGUID, "CoreFoundation.framework", false);

            // Write changes to the project file.
            proj.WriteToFile(projPath);

            Debug.Log("AddCoreBluetoothIOS: CoreBluetooth.framework has been added to the Xcode project.");
        }
    }
}
#endif