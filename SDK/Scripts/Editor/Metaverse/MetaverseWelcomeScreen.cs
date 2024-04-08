using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    internal class MetaverseWelcomeScreen : EditorWindow
    {
        private const string MetaverseWelcomeScreenKey = "MetaverseWelcomeScreen.v5";
        
        [InitializeOnLoadMethod]
        private static void Init()
        {
            if (!EditorPrefs.GetBool(MetaverseWelcomeScreenKey, true)) return;
            EditorPrefs.SetBool(MetaverseWelcomeScreenKey, false);
            ShowWindow();
        }

        private static void ShowWindow()
        {
            var window = GetWindow<MetaverseWelcomeScreen>(true, "Metaverse Cloud Engine SDK - Welcome");
            window.maxSize = new Vector2(600, 500);
            window.minSize = window.maxSize;
        }

        private void OnGUI()
        {
            MetaverseEditorUtils.Header("Metaverse Cloud Engine (Alpha)");

            EditorGUILayout.LabelField("Welcome to the Metaverse Cloud Engine!", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "Thank you for choosing to use our SDK!", 
                EditorStyles.label);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("Our SDK is currently in Alpha. You may experience bugs or breaking changes. " +
                                    "Please report any issues you find to our Discord server.", MessageType.Warning);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PrefixLabel("Documentation");
            if (EditorGUILayout.LinkButton(
                    "https://reach-cloud.gitbook.io/reach-explorer-documentation/building-on-reach/unity-engine-sdk",
                    GUILayout.Width(500)))
                Application.OpenURL(
                    "https://reach-cloud.gitbook.io/reach-explorer-documentation/building-on-reach/unity-engine-sdk");
            
            EditorGUILayout.Space();

            EditorGUILayout.PrefixLabel("Discord");
            if (EditorGUILayout.LinkButton("https://discord.gg/Eg7fUNJGk6"))
                Application.OpenURL("https://discord.gg/Eg7fUNJGk6");
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PrefixLabel("Website");
            if (EditorGUILayout.LinkButton("https://reachcloud.org"))
                Application.OpenURL("https://reachcloud.org");
            
            GUILayout.Space(35);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Start Building", GUILayout.Width(100), GUILayout.Height(30)))
            {
                Close();
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}