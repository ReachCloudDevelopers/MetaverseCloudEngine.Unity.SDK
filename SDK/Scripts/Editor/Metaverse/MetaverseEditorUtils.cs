using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MetaverseCloudEngine.Unity.XR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
#if !UNITY_IOS
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
#endif
using UnityEngine.XR.OpenXR.Features.OculusQuestSupport;
#endif

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseEditorUtils
    {
        private static GUIStyle _headerStyle;
        public static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            imagePosition = ImagePosition.ImageLeft
        };

        private static GUIStyle _textFieldStyle;
        public static GUIStyle TextFieldStyle => _textFieldStyle ?? new GUIStyle(EditorStyles.textField);

        private static GUIStyle _editorIconStyle;
        public static GUIStyle EditorIconStyle => _editorIconStyle ??= new GUIStyle()
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 70,
            fontStyle = FontStyle.Bold,
            imagePosition = ImagePosition.ImageOnly,
        };

        private static Texture2D _editorIcon;

        public static Texture2D EditorIcon => _editorIcon
            ? _editorIcon
            : Resources.Load<Texture2D>(MetaverseConstants.Resources.ResourcesBasePath + "EditorIcon");

        public static void Disabled(Action draw, bool isDisabled = true)
        {
            var wasEnabled = GUI.enabled;
            GUI.enabled = !isDisabled;
            draw?.Invoke();
            GUI.enabled = wasEnabled;
        }

        public static void Box(Action draw, string style = "box", bool vertical = true)
        {
            if (vertical)
            {
                if (string.IsNullOrEmpty(style))
                    EditorGUILayout.BeginVertical();
                else
                    EditorGUILayout.BeginVertical(style);
            }
            else
            {
                if (string.IsNullOrEmpty(style))
                    EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                else
                    EditorGUILayout.BeginHorizontal(style, GUILayout.ExpandWidth(true));
            }

            draw?.Invoke();
            
            if (vertical)
                EditorGUILayout.EndVertical();
            else
                EditorGUILayout.EndHorizontal();
        }

        public static void Header(string text, bool displayIcon = true)
        {
            Header(new GUIContent(text), displayIcon);
        }

        public static void Header(GUIContent content, bool displayIcon = true)
        {
            EditorGUILayout.BeginVertical("box");
            if (EditorIcon != null && displayIcon)
            {
                EditorGUIUtility.SetIconSize(Vector2.one * 50);
                try
                {
                    EditorGUILayout.Space(20);
                    EditorGUILayout.LabelField(new GUIContent(EditorIcon), EditorIconStyle);
                    EditorGUILayout.Space(20);
                }
                finally
                {
                    EditorGUIUtility.SetIconSize(Vector2.zero);
                }
            }
            EditorGUILayout.LabelField(content, HeaderStyle, GUILayout.Height(50));
            EditorGUILayout.EndVertical();
        }

        public static void Error(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            EditorGUILayout.HelpBox(text, MessageType.Error);
        }

        public static void Info(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                EditorGUILayout.HelpBox(text, MessageType.Info);
            }
            catch (Exception)
            {
                GUIUtility.ExitGUI();
            }
        }

        public static string TextField(string label, string text, bool @protected = false)
        {
            if (!@protected)
                return EditorGUILayout.TextField(label, text, TextFieldStyle);
            return EditorGUILayout.PasswordField(label, text, TextFieldStyle);
        }

        public static string TextArea(string label, string text)
        {
            EditorGUILayout.PrefixLabel(label);
            return EditorGUILayout.TextArea(text, GUILayout.Height(50));
        }

        public static void DrawDefaultLoadingScreen()
        {
            EditorGUILayout.HelpBox("Please wait...", MessageType.Info);
        }

        public static void DrawLoadingScreen(Action drawDefault, Action drawLoading, bool isLoading, bool drawOnTop = false)
        {
            if (isLoading)
                drawLoading?.Invoke();
            else if (!drawOnTop)
                drawDefault?.Invoke();

            if (drawOnTop)
            {
                if (isLoading) Disabled(drawDefault);
                else drawDefault?.Invoke();
            }
        }

        public static void ConfigureGraphicsSettings(bool enabled)
        {
            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            var serObj = new SerializedObject(graphicsSettings);

            serObj.FindProperty("m_LightmapStripping").intValue = enabled ? 1 : 0;
            if (enabled)
            {
                // serObj.FindProperty("m_LightmapKeepPlain").boolValue = true;
                // serObj.FindProperty("m_LightmapKeepDirCombined").boolValue = true;
                // serObj.FindProperty("m_LightmapKeepDynamicPlain").boolValue = true;
                // serObj.FindProperty("m_LightmapKeepDynamicDirCombined").boolValue = true;
                serObj.FindProperty("m_LightmapKeepShadowMask").boolValue = true;
                // serObj.FindProperty("m_LightmapKeepSubtractive").boolValue = true;
            }

            serObj.FindProperty("m_FogStripping").intValue = enabled ? 1 : 0;
            if (enabled)
            {
                serObj.FindProperty("m_FogKeepLinear").boolValue = true;
                // serObj.FindProperty("m_FogKeepExp").boolValue = true;
                // serObj.FindProperty("m_FogKeepExp2").boolValue = true;
            }

            serObj.FindProperty("m_InstancingStripping").intValue = enabled ? 2 : 0;

            serObj.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(graphicsSettings);
        }

        public static bool IsOldInputManagerDisabled()
        {
            var obj = typeof(PlayerSettings).GetMethod("GetDisableOldInputManagerSupport", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, Array.Empty<object>());
            return obj is true;
        }

        public static bool ConfigureXRLoaders(
            BuildTargetGroup group,
            string[] xrLoaders,
            XRSDK xrsdk,
            bool changeInitOnStartup = true,
            bool initOnStartup = true)
        {
            bool configured;
            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var btg in (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup)))
                {
                    var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                    if (settings == null)
                        continue;

                    var changed = false;
                    if (changeInitOnStartup)
                        if (settings.InitManagerOnStart)
                        {
                            changed = true;
                            settings.InitManagerOnStart = false;
                        }

                    if (settings.AssignedSettings != null)
                        foreach (var loader in settings.AssignedSettings.activeLoaders.ToArray())
                            if (settings.AssignedSettings.TryRemoveLoader(loader))
                                changed = true;

                    if (changed)
                        EditorUtility.SetDirty(settings);

#if !UNITY_IOS
                    var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(btg);
                    if (openXRSettings != null)
                    {
                        changed = false;
                        foreach (var feat in openXRSettings.GetFeatures().ToArray())
                            if (feat && feat.enabled)
                            {
                                changed = true;
                                feat.enabled = false;
                            }

                        if (changed)
                            EditorUtility.SetDirty(openXRSettings);
                    }
#endif

                    if (changed)
                        AssetDatabase.SaveAssets();
                }

                var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                if (xrSettings == null)
                    return false;

                if (changeInitOnStartup)
                    xrSettings.InitManagerOnStart = xrLoaders is { Length: > 0 } && initOnStartup;
                if (xrSettings.AssignedSettings == null)
                    return false;

                configured = xrLoaders is not { Length: > 0 } || xrLoaders.All(loader => XRPackageMetadataStore.AssignLoader(xrSettings.Manager, loader, group));
                if (configured)
                {
#if !UNITY_IOS
                    var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                    if (openXRSettings != null)
                    {
                        openXRSettings.renderMode = OpenXRSettings.RenderMode.MultiPass; // Enforce multi pass rendering.
                        var usingOpenXR = xrLoaders?.Contains(typeof(OpenXRLoader).FullName) == true;
                        
#if MV_META_CORE && METAVERSE_CLOUD_ENGINE_INTERNAL
                        ToggleOpenXRFeature<MetaQuestFeature>(openXRSettings, usingOpenXR && group == BuildTargetGroup.Android);
                        var metaXRFeatureSet = UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager.GetFeatureSetWithId(
                            BuildTargetGroup.Standalone, "com.meta.openxr.featureset.metaxr");
                        metaXRFeatureSet.isEnabled = false;
                        UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.Standalone);
                        ToggleOpenXRFeature<Meta.XR.MetaXRFeature>(openXRSettings, false);
                        ToggleOpenXRFeature<Meta.XR.MetaXRSubsampledLayout>(openXRSettings, false);
                        ToggleOpenXRFeature<Meta.XR.MetaXRFoveationFeature>(openXRSettings, false);
                        ToggleOpenXRFeature<Meta.XR.MetaXREyeTrackedFoveationFeature>(openXRSettings, false);
#endif
#if MV_XR_HANDS
                        ToggleOpenXRFeature<UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim>(openXRSettings, usingOpenXR);
                        ToggleOpenXRFeature<UnityEngine.XR.Hands.OpenXR.HandTracking>(openXRSettings, usingOpenXR);                            
                        if (group == BuildTargetGroup.Standalone)
                        {
                            ToggleOpenXRFeature<HandInteractionProfile>(openXRSettings, usingOpenXR);
                            ToggleOpenXRFeature<HandCommonPosesInteraction>(openXRSettings, usingOpenXR);
                        }
#endif
                        ToggleOpenXRFeature<MetaQuestTouchProControllerProfile>(openXRSettings, usingOpenXR);
                        ToggleOpenXRFeature<OculusTouchControllerProfile>(openXRSettings, usingOpenXR);
                        ToggleOpenXRFeature<HTCViveControllerProfile>(openXRSettings, usingOpenXR);
                        ToggleOpenXRFeature<ValveIndexControllerProfile>(openXRSettings, usingOpenXR);
                    }
#endif
                }

                EditorUtility.SetDirty(xrSettings);
                EditorUtility.SetDirty(xrSettings.AssignedSettings);

                AssetDatabase.SaveAssets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return configured;
        }

#if !UNITY_IOS
        private static void ToggleOpenXRFeature<TFeature>(OpenXRSettings buildTargetInstance, bool enable)
            where TFeature : OpenXRFeature
        {
            OpenXRFeature feature = buildTargetInstance.GetFeature<TFeature>();
            if (feature == null) return;
            if (feature.enabled == enable) return;
            feature.enabled = enable;
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(feature);
        }
#endif

        public static byte[] EncodeToBytes(this Texture2D image)
        {
            try
            {
                var data = Array.Empty<byte>();
                var filePath = AssetDatabase.GetAssetPath(image);
                var extension = "png";
                if (!string.IsNullOrEmpty(filePath))
                    extension = Path.GetExtension(filePath).ToLower().Replace(".", string.Empty);

                switch (extension)
                {
                    case "jpg":
                    case "jpeg": data = image.EncodeToJPG(); break;
                    case "png": data = image.EncodeToPNG(); break;
                    case "exr": data = image.EncodeToEXR(); break;
                    case "tga": data = image.EncodeToTGA(); break;
                }

                if (data == null || data.Length == 0)
                    throw new InvalidOperationException();

                return data;
            }
            catch
            {
                throw new Exception("Failed to encode thumbnail. Please ensure the file type is jpg, jpeg, png, exr, or tga.");
            }
        }

        public static bool IsXrLoaderConfigured(BuildTargetGroup group, string name)
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            if (settings == null || settings.AssignedSettings == null) return false;
            var assignedSettingsActiveLoaders = settings.AssignedSettings.activeLoaders;
            return assignedSettingsActiveLoaders != null && assignedSettingsActiveLoaders.FirstOrDefault(x => x && x.GetType().FullName == name);
        }

        public static IEnumerable<BuildTargetGroup> GetSupportedBuildTargetGroups()
        {
            return ((BuildTarget[])Enum.GetValues(typeof(BuildTarget)))
                .Select(x => new { group = BuildPipeline.GetBuildTargetGroup(x), target = x })
                .Where(x => BuildPipeline.IsBuildTargetSupported(x.group, x.target))
                .Select(x => x.group)
                .Distinct()
                .ToArray();
        }

        public static void EditorFrameDelay(Action action, int frames = 1)
        {
            EditorApplication.update += OnFinish;
            return;

            void OnFinish()
            {
                EditorApplication.update -= OnFinish;

                if (frames == 0)
                {
                    action?.Invoke();
                    return;
                }

                EditorFrameDelay(action, --frames);
            }
        }
    }
}