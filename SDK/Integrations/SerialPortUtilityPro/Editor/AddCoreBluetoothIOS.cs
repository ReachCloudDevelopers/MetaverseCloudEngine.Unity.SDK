#if UNITY_IOS
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using System.IO;
using MetaverseCloudEngine.Unity;

public class AddCoreBluetoothIOS
{
    [UnityEditor.Callbacks.PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.iOS)
        {
            // Path to the Xcode project.
            var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Get the target GUID. For newer Unity versions, GetUnityMainTargetGuid() is recommended.
            var targetGUID = proj.GetUnityFrameworkTargetGuid();

            // Add the CoreBluetooth framework.
            proj.AddFrameworkToProject(targetGUID, "CoreBluetooth.framework", false);

            // Write changes to the project file.
            proj.WriteToFile(projPath);

            MetaverseProgram.Logger.Log("AddCoreBluetoothIOS: CoreBluetooth.framework has been added to the Xcode project.");
            
            // Enable in Info.plist the NSBluetoothAlwaysUsageDescription key
            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var rootDict = plist.root;
            if (!rootDict.values.ContainsKey("NSBluetoothAlwaysUsageDescription"))
            {
                rootDict.SetString("NSBluetoothAlwaysUsageDescription", "This app requires Bluetooth access to connect to devices.");
                plist.WriteToFile(plistPath);
                MetaverseProgram.Logger.Log("AddCoreBluetoothIOS: NSBluetoothAlwaysUsageDescription key has been added to Info.plist.");
            }
            else
            {
                MetaverseProgram.Logger.Log("AddCoreBluetoothIOS: NSBluetoothAlwaysUsageDescription key already exists in Info.plist.");
            }
        }
    }
}
#endif