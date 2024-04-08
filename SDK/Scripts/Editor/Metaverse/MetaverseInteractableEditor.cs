using MetaverseCloudEngine.Unity.XR.Components;
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
        private bool m_FoldoutEvents;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.UpdateIfRequiredOrScript();

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