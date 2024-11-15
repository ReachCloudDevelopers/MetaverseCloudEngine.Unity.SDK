using System.Linq;
using TriInspectorMVCE;
using UnityEditor;
using UnityEngine;
using MetaverseCloudEngine.Unity.Scripting.Components;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomEditor(typeof(MetaverseScript), editorForChildClasses: true, isFallback = true)]
    internal class MetaverseScriptEditor : TriMonoBehaviourEditor
    {
        private UnityEditor.Editor _variablesEditor;

        public override void OnInspectorGUI()
        {
            var javascriptFileProp = serializedObject.FindProperty("javascriptFile");
            MetaverseEditorUtils.Header(javascriptFileProp.objectReferenceValue != null ? javascriptFileProp.objectReferenceValue.name : "(No Script)", false);

            var variablesProp = serializedObject.FindProperty("variables"); // This property references a visual scripting variables component.
            
            base.OnInspectorGUI();

            GUILayout.Space(15);

            if (variablesProp.objectReferenceValue != null && 
                variablesProp.objectReferenceValue is Component c && 
                c.gameObject != (target as MonoBehaviour)?.gameObject)
            {
                RenderVariablesEditor(variablesProp);
            }
        }

        private void RenderVariablesEditor(SerializedProperty variablesProp)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(new GUIContent("Variables", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image), EditorStyles.boldLabel); 
            EditorGUILayout.EndHorizontal();
            
            // Make it so that when we start dragging the variables header, it beings a drag operation
            // for the variables component itself.
            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { variablesProp.objectReferenceValue };
                DragAndDrop.StartDrag("Variables");
                Event.current.Use();
            }
            
            CreateCachedEditor(variablesProp.objectReferenceValue, null, ref _variablesEditor);

            _variablesEditor.OnInspectorGUI();

            if (!variablesProp.objectReferenceValue)
                return;
            
            // Check if the target game object is a part of a prefab.
            // If it is (and this is a prefab instance in a scene) then 
            // render a "Apply" button to apply the changes to the prefab.
            if (!PrefabUtility.IsPartOfPrefabInstance(variablesProp.objectReferenceValue)) 
                return;
            
            var source = PrefabUtility.GetCorrespondingObjectFromSource(variablesProp.objectReferenceValue);
            var modifications = PrefabUtility.GetPropertyModifications(variablesProp.objectReferenceValue);
            var modifiedProperties = modifications.Where(mod => mod.target == source).ToArray();
            if (modifiedProperties.Length == 0) 
                return;

            if (GUILayout.Button("Apply Changes"))
            {
                foreach (var modifiedProperty in modifiedProperties)
                {
                    var path = modifiedProperty.propertyPath;
                    var property = new SerializedObject(variablesProp.objectReferenceValue).FindProperty(path);
                    PrefabUtility.ApplyPropertyOverride(
                        property,
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(variablesProp.objectReferenceValue),
                        InteractionMode.UserAction);
                }
            }
            else if (GUILayout.Button("Revert Changes"))
            {
                foreach (var modifiedProperty in modifiedProperties)
                {
                    var path = modifiedProperty.propertyPath;
                    var property = new SerializedObject(variablesProp.objectReferenceValue).FindProperty(path);
                    PrefabUtility.RevertPropertyOverride(
                        property,
                        InteractionMode.UserAction);
                }
            }
        }
    }
}
