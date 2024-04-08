using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    internal static class CreateJavascriptFile
    {
        [MenuItem("Assets/Create/Javascript File", priority = 80)]
        static void CreateJS()
        {
            var obj = Selection.activeObject;
            string path = obj == null ? "Assets" : AssetDatabase.GetAssetPath(obj.GetInstanceID());
            if (path.Length == 0)
                return;

            if (!Directory.Exists(path))
                path = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(path))
            {
                var desiredPath = path + $"/New Javascript File.js";
                var outputPath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
                ProjectWindowUtil.CreateAssetWithContent(outputPath, string.Empty, MetaverseEditorUtils.EditorIcon);
            }
        }
    }

#if UNITY_2022_2_OR_NEWER
    [ScriptedImporter(1, new [] { "js", "ts" })]
    public class JavascriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var existingAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ctx.assetPath);
            if (existingAsset != null)
                return;
            
            var textAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("Script", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
#endif
}