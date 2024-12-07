using System;
using System.Linq;
using UnityEditor;
#pragma warning disable CS0219 // Variable is assigned but its value is never used

namespace MetaverseCloudEngine.Unity.Editors
{
    public class PreventSDKAssetsFromBeingEdited : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            var didPreventSave = false;
            foreach (var path in paths.ToArray())
            {
                if (!path.EndsWith("SDK/Demo/Prefabs/Demo Level.prefab") &&
                    !path.Contains("Editor/Resources/Metaverse SDK")) 
                    continue;

#if METAVERSE_CLOUD_ENGINE_INTERNAL
                if (!didPreventSave && EditorUtility.DisplayDialog("Metaverse Cloud SDK",
                        "Override SDK asset protection?", "Yes", "No"))
                    return paths;
#endif
                didPreventSave = true;
                
                paths = paths.Except(new[] {path}).ToArray();
            }
            
            return paths;
        }
    }
}