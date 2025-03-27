#if METAVERSE_CLOUD_ENGINE && METAVERSE_CLOUD_ENGINE_INITIALIZED && MV_SDK_DEV
using System.Linq;
using UnityEditor;
using UnityEngine;  

namespace MetaverseCloudEngine.Unity.Editors.Builds
{
    public static class MetaverseScriptIcons
    {
        [MenuItem(MetaverseConstants.MenuItems.MenuRootPath + "Dev/Update Editor Icons")]
        public static void UpdateEditorIcons()
        {
            var editorIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(MetaverseEditorUtils.EditorIcon));
            if (editorIcon == null) 
                return;
            
            var behaviours = AssetDatabase.FindAssets("t:Script")
                .Select(x => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(x)))
                .Where(x =>
                {
                    if (!x) return false;
                    var ns = x.GetClass()?.Namespace;
                    return ns != null &&
                           ns.StartsWith("MetaverseCloudEngine.Unity") &&
                           typeof(MonoBehaviour).IsAssignableFrom(x.GetClass());
                })
                .ToArray();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var o in behaviours)
                {
                    var importer = (MonoImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o));
                    importer.SetIcon(editorIcon);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
#endif // METAVERSE_CLOUD_ENGINE