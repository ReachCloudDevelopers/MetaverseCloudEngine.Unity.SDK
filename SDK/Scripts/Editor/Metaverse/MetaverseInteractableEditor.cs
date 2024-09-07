using System.Linq;
using MetaverseCloudEngine.Unity.XR.Components;
using TriInspectorMVCE.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(SelectEnterEvent))]
    [CustomPropertyDrawer(typeof(SelectExitEvent))]
    [CustomPropertyDrawer(typeof(HoverEnterEvent))]
    [CustomPropertyDrawer(typeof(HoverExitEvent))]
    [CustomPropertyDrawer(typeof(FocusEnterEvent))]
    [CustomPropertyDrawer(typeof(FocusExitEvent))]
    [CustomPropertyDrawer(typeof(ActivateEvent))]
    [CustomPropertyDrawer(typeof(DeactivateEvent))]
#pragma warning disable CS0618 // Type or member is obsolete
    [CustomPropertyDrawer(typeof(XRInteractableEvent))]
#pragma warning restore CS0618 // Type or member is obsolete
    public class MetaverseInteractableHideEventsPropertyDrawer : PropertyDrawer
    {
        private readonly UnityEventCompactDrawer m_EventCompactDrawer = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (MetaverseInteractableEditor.m_DrawingEvents || property.serializedObject.targetObject is not MetaverseInteractable)
            {
                return m_EventCompactDrawer.GetPropertyHeight(property, label);
            }
            return -2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (MetaverseInteractableEditor.m_DrawingEvents || property.serializedObject.targetObject is not MetaverseInteractable)
            {
                m_EventCompactDrawer.OnGUI(position, property, label);
            }
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(MetaverseInteractable))]
    public class MetaverseInteractableEditor : TriInspectorMVCE.TriEditor
    {
        internal static bool m_DrawingEvents;
        private static bool m_FoldoutEvents;
        private static bool m_FoldoutGazeSettings;
        private static bool m_FoldoutMetaverseSettings;

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            MetaverseEditorUtils.Header("Metaverse Interactable");
            MetaverseEditorUtils.Info("This component is used to make an object interactable with the Metaverse Cloud SDK.");

            if (EditorGUILayout.LinkButton("Metaverse SDK Documentation"))
                Application.OpenURL("https://reach-cloud.gitbook.io/reach-explorer-documentation/metaverse-cloud-engine-sdk/unity-engine-sdk/components/interactions/metaverse-interactable");
            EditorGUILayout.Space(10);
            
            if (EditorGUILayout.LinkButton("Unity XR Interaction Toolkit Documentation"))
                Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest");
            EditorGUILayout.Space(10);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_InteractionManager"), 
                new GUIContent("Interaction Manager (Optional)", tooltip: "The Unity XR Interaction Manager to use for this interactable. This will be automatically set if not set."),
                true);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Interaction Physics", EditorStyles.largeLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_InteractionLayerMask"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_InteractionLayers"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DistanceCalculationMode"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Colliders"), true);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Interaction Modes", EditorStyles.largeLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SelectMode"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FocusMode"), true);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Visuals", EditorStyles.largeLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CustomReticle"), true);
            
            EditorGUILayout.Space(10);
            m_FoldoutGazeSettings = EditorGUILayout.Foldout(m_FoldoutGazeSettings, "Gaze Settings");
            if (m_FoldoutGazeSettings)
            {
                EditorGUI.indentLevel++;
                try
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AllowGazeInteraction"), true);
                    if (serializedObject.FindProperty("m_AllowGazeInteraction").boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AllowGazeSelect"), true);
                        if (serializedObject.FindProperty("m_AllowGazeSelect").boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OverrideGazeTimeToSelect"), true);
                            if (serializedObject.FindProperty("m_OverrideGazeTimeToSelect").boolValue)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_GazeTimeToSelect"), true);
                                EditorGUI.indentLevel--;
                            }
                            
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_OverrideTimeToAutoDeselectGaze"), true);
                            if (serializedObject.FindProperty("m_OverrideTimeToAutoDeselectGaze").boolValue)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TimeToAutoDeselectGaze"), true);
                                EditorGUI.indentLevel--;
                            }
                            
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }
                    
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AllowGazeAssistance"), true);
                }
                finally
                {
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUILayout.Space(10);
            m_FoldoutMetaverseSettings = EditorGUILayout.Foldout(m_FoldoutMetaverseSettings, "Metaverse Settings");
            if (m_FoldoutMetaverseSettings)
            {
                try
                {
                    EditorGUI.indentLevel++;
                    
                    var fieldsOnScript = serializedObject.targetObject.GetType().GetFields(
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public)
                        .Where(x => x.GetCustomAttributes(typeof(SerializeField), true).Length > 0);

                    foreach (var field in fieldsOnScript)
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(field.Name), true);
                }
                finally
                {
                    EditorGUI.indentLevel--;
                }
            }
            
            // Draw Events
            EditorGUILayout.Space(10);

            m_DrawingEvents = true;
            try
            {
                m_FoldoutEvents = EditorGUILayout.Foldout(m_FoldoutEvents, "Interactable Events");

                if (m_FoldoutEvents)
                {
                    EditorGUI.indentLevel++;

                    try
                    {
                        EditorGUILayout.LabelField("Select Enter / Exit", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FirstSelectEntered"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SelectEntered"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LastSelectExited"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SelectExited"), true);

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Hover Enter / Exit", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FirstHoverEntered"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HoverEntered"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LastHoverExited"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HoverExited"), true);
                        
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Focus Enter / Exit", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FirstFocusEntered"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FocusEntered"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LastFocusExited"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_FocusExited"), true);

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Activate / Deactivate", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Activated"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Deactivated"), true);
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
            }
            finally
            {
                m_DrawingEvents = false;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}