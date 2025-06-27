#if !MV_UNITY_AI_INFERENCE && UNITY_EDITOR
using TriInspectorMVCE;
using UnityEditor;
using UnityEditor.PackageManager;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    public class InferenceEngineComponent : TriInspectorMonoBehaviour
    {
        [Button("Install 'com.unity.ai.inference' Package")]
        private void InstallInferenceEnginePackage()
        {
            if (!EditorUtility.DisplayDialog("Install Inference Engine Package",
                    "This will install the com.unity.ai.inference package. Do you want to proceed?",
                    "Yes", "No"))
                return; 

            const string packageName = "com.unity.ai.inference@2.2.0";
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath(packageName) == null)
            {
                var addReq = Client.Add(packageName);
                while (!addReq.IsCompleted)
                {
                    // Wait for the package to be added
                    EditorApplication.Step();
                }
                
                if (addReq.Status == StatusCode.Success)
                    EditorUtility.DisplayDialog("Package Installation",
                        $"The {packageName} package has been added to your project.", "OK");
                else if (addReq.Status == StatusCode.Failure)
                    EditorUtility.DisplayDialog("Package Installation Failed",
                        $"Failed to install {packageName}: {addReq.Error.message}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Package Already Installed",
                    $"The {packageName} package is already installed in your project.", "OK");
            }
            
            EditorApplication.OpenProject(System.Environment.CurrentDirectory);
        }
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class InferenceEngineComponent : TriInspectorMVCE.TriInspectorMonoBehaviour
    {
        // The package is successfully installed.
    }
}
#endif