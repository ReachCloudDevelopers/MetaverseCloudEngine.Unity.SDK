using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    // TODO: Fix this.
    public class ProjectOptimizationManager : EditorWindow
    {
        private const string TextureCompressionPrefKey = "POM_TextureCompressionQuality";
        private const string MeshCompressionPrefKey = "POM_MeshCompressionQuality";
        private const string MaximumTextureResolutionPrefKey = "POM_TextureResolutionKey";
        private const string TooltipsEnabledKey = "POM_Tooltips";

        private Vector3 _scroll;

        private static readonly string[] TextureResolutions = new string[]
        {
            "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192", "16384"
        };

        private static readonly int[] TextureResolutionValues = new int[]
        {
            32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384
        };

        private bool TooltipsEnabled {
            get => EditorPrefs.GetBool(TooltipsEnabledKey, false);
            set => EditorPrefs.SetBool(TooltipsEnabledKey, value);
        }

        private int TextureCompressionQuality {
            get => EditorPrefs.GetInt(TextureCompressionPrefKey, 50);
            set => EditorPrefs.SetInt(TextureCompressionPrefKey, value);
        }

        private ModelImporterMeshCompression MeshCompression {
            get => (ModelImporterMeshCompression)EditorPrefs.GetInt(MeshCompressionPrefKey, (int)ModelImporterMeshCompression.Medium);
            set => EditorPrefs.SetInt(MeshCompressionPrefKey, (int)value);
        }

        private int MaximumTextureResolution {
            get => EditorPrefs.GetInt(MaximumTextureResolutionPrefKey, 2048);
            set => EditorPrefs.SetInt(MaximumTextureResolutionPrefKey, value);
        }

        //[MenuItem(MetaverseConstants.MenuItems.ToolsMenuRootPath + "Project Optimizer")]
        static void Init()
        {
            var window = GetWindow<ProjectOptimizationManager>("Project Optimizer");
            window.ShowUtility();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {

                MetaverseEditorUtils.Header("Project Optimizer");
                TooltipsEnabled = EditorGUILayout.Toggle("Show Help", TooltipsEnabled);

                if (TooltipsEnabled)
                    MetaverseEditorUtils.Info("Using the settings below you can perform project-wide texture & mesh optimization.");

                MetaverseEditorUtils.Box(() =>
                {
                    MetaverseEditorUtils.Box(() => EditorGUILayout.LabelField("Quality Presets", EditorStyles.miniBoldLabel), style: "toolbar");
                    MetaverseEditorUtils.Box(() =>
                    {
                        if (GUILayout.Button("Low"))
                        {
                            TextureCompressionQuality = 0;
                            MaximumTextureResolution = 512;
                            MeshCompression = ModelImporterMeshCompression.High;
                        }

                        if (GUILayout.Button("Medium"))
                        {
                            TextureCompressionQuality = 10;
                            MaximumTextureResolution = 1024;
                            MeshCompression = ModelImporterMeshCompression.Medium;
                        }

                        if (GUILayout.Button("High"))
                        {
                            TextureCompressionQuality = 35;
                            MaximumTextureResolution = 2048;
                            MeshCompression = ModelImporterMeshCompression.Low;
                        }

                        if (GUILayout.Button("Ultra"))
                        {
                            TextureCompressionQuality = 50;
                            MaximumTextureResolution = 4096;
                            MeshCompression = ModelImporterMeshCompression.Off;
                        }

                    }, vertical: false);
                });

                MetaverseEditorUtils.Box(() =>
                {
                    MetaverseEditorUtils.Box(() => EditorGUILayout.LabelField("Textures", EditorStyles.miniBoldLabel), style: "toolbar");
                    if (TooltipsEnabled)
                        MetaverseEditorUtils.Info("The lower the compression quality the smaller the texture memory footprint.");
                    TextureCompressionQuality = EditorGUILayout.IntSlider("Compression Quality %", TextureCompressionQuality, 0, 100);

                    if (TooltipsEnabled)
                        MetaverseEditorUtils.Info("Set the maximum texture resolution to lower values to reduce memory footprint at the cost of visual fidelity.");
                    MaximumTextureResolution = EditorGUILayout.IntPopup("Max Size", MaximumTextureResolution, TextureResolutions, TextureResolutionValues);

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Optimize Textures") && EditorUtility.DisplayDialog("Optimize Textures", "This process may take a long time. Are you sure you want to do that?", "Yes", "Cancel"))
                    {
                        OptimizeTextures();
                    }
                });

                MetaverseEditorUtils.Box(() =>
                {
                    MetaverseEditorUtils.Box(() => EditorGUILayout.LabelField("Meshes", EditorStyles.miniBoldLabel), style: "toolbar");
                    if (TooltipsEnabled)
                        MetaverseEditorUtils.Info("The higher the mesh compression, the smaller the memory footprint (at the cost of visual quality).");
                    MeshCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup("Compression Level", MeshCompression);

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Optimize Meshes") && EditorUtility.DisplayDialog("Optimize Meshes", "This process may take a long time. Are you sure you want to do that?", "Yes", "Cancel"))
                    {
                        OptimizeMeshes();
                    }
                });
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Optimize All"))
            {
                OptimizeTextures();
                OptimizeMeshes();
            }
        }

        private void OptimizeMeshes()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                var textures = AssetDatabase.FindAssets("t:Model");
                foreach (var modelGUID in textures)
                {
                    var modelImporter = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(modelGUID)) as ModelImporter;
                    if (modelImporter == null) continue;
                    modelImporter.meshCompression = MeshCompression;
                    modelImporter.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
                    modelImporter.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private void OptimizeTextures()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                var textures = AssetDatabase.FindAssets("t:Texture2D");
                foreach (var textureGUID in textures)
                {
                    var textureImporter = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(textureGUID)) as TextureImporter;
                    if (textureImporter == null) continue;
                    if (textureImporter.crunchedCompression != true ||
                        textureImporter.compressionQuality != TextureCompressionQuality ||
                        textureImporter.maxTextureSize != MaximumTextureResolution)
                        continue;
                    textureImporter.crunchedCompression = true;
                    textureImporter.compressionQuality = TextureCompressionQuality;
                    textureImporter.maxTextureSize = MaximumTextureResolution;
                    textureImporter.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
