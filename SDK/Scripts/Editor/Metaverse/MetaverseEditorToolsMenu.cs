using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseEditorToolsMenu
    {
        [MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "Cache/Clear Download Cache")]
        public static void ClearCache()
        {
            if (!EditorUtility.DisplayDialog("Clear Cache Warning", $"You are about to clear {Caching.currentCacheForWriting.spaceOccupied / (1024 * 1024)} MB from your cache. Are you sure you want to do this?", "Yes", "Cancel"))
                return;

            var totalSpaceOccupied = Caching.currentCacheForWriting.spaceOccupied;
            if (Caching.currentCacheForWriting.ClearCache())
            {
                Debug.Log($"<b><color=green>Successfully</color></b> cleared {totalSpaceOccupied} bytes from the cache.");
                return;
            }

            Debug.LogError("Clearing cache <b><color=red>failed</color></b>.");
        }

        [MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "Cache/Purge Build Cache")]
        public static void ClearBuildCache()
        {
            BuildCache.PurgeCache(true);
        }

        [MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "Cache/Delete All Player Prefs")]
        public static void DeletePlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
        }

        [MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "/Unity Editor/Reload Script Assembly")]
        public static void ReloadAssemblies()
        {
            EditorUtility.RequestScriptReload();
        }

        [MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "Unity Editor/Remove Missing Scripts")]
        [MenuItem("Assets/Selection/Remove Missing Scripts")]
        public static void RemoveMissingScriptsFromSelectedObjects()
        {
            var total = 0;
            foreach (var obj in Selection.gameObjects.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Select(x => x.gameObject))
            {
                var count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                total += count;
            }

            Debug.Log("Done removing missing scripts! Removed " + total + " missing script(s).");
        }

        [MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "Unity Editor/Restart Unity")]
        public static void RestartUnity()
        {
            static void RestartProj() { EditorApplication.update -= RestartProj; EditorApplication.OpenProject(Environment.CurrentDirectory); }
            EditorApplication.update += RestartProj;
        }

        [MenuItem("Assets/Selection/Select All Models")]
        public static void SelectAllModels()
        {
            var assets = AssetDatabase.FindAssets("t:Model")
                .Where(x => AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(x)) as ModelImporter != null)
                .Where(x => AssetDatabase.GUIDToAssetPath(x).StartsWith("Assets/"))
                .Select(x => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(x)));
            Selection.objects = assets.ToArray();
        }

        [MenuItem("Assets/Selection/Select All Textures")]
        public static void SelectAllTextures()
        {
            var assets = AssetDatabase.FindAssets("t:Texture2D")
                .Where(x => AssetDatabase.GUIDToAssetPath(x).StartsWith("Assets/"))
                .Select(x => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(x)));
            Selection.objects = assets.ToArray();
        }

        [MenuItem("Assets/Selection/Select All Textures (Ignore Normal Maps)")]
        public static void SelectAllNonNormalMapTextures()
        {
            var assets = AssetDatabase.FindAssets("t:Texture2D")
                .Where(x => (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(x)) as TextureImporter)?.textureType is TextureImporterType.Default or TextureImporterType.Sprite or TextureImporterType.Lightmap or TextureImporterType.DirectionalLightmap)
                .Where(x => AssetDatabase.GUIDToAssetPath(x).StartsWith("Assets/"))
                .Select(x => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(x)));
            Selection.objects = assets.ToArray();
        }

        [MenuItem("Assets/Selection/Select All Textures (Only Normal Maps)")]
        public static void SelectAllNormalMapTextures()
        {
            var assets = AssetDatabase.FindAssets("t:Texture2D")
                .Where(x => (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(x)) as TextureImporter)?.textureType == TextureImporterType.NormalMap)
                .Where(x => AssetDatabase.GUIDToAssetPath(x).StartsWith("Assets/"))
                .Select(x => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(x)));
            Selection.objects = assets.ToArray();
        }
    }
}
