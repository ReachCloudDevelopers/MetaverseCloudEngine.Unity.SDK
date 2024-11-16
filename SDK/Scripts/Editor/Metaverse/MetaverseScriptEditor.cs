using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
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
            MetaverseEditorUtils.Header(javascriptFileProp.objectReferenceValue != null 
                ? javascriptFileProp.objectReferenceValue.name 
                : "(No Script)", false);

            if (!javascriptFileProp.objectReferenceValue)
            {
                if (GUILayout.Button("Create New Script"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Create New Script", "NewScript", "js", "Create a new script file");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var script = new TextAsset();
                        System.IO.File.WriteAllText(path, 
@"// This is a new script file. You can write your script here.
// For more information on how to write scripts, visit the documentation at https://docs.reachcloud.org/
const UnityEngine = importNamespace('UnityEngine');
const MetaverseCloudEngine = importNamespace('MetaverseCloudEngine');

// This function is called when the script is started.
function Start() {

}

// This function is called every frame.
function Update() {

}
");
                        AssetDatabase.ImportAsset(path);
                        javascriptFileProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                        javascriptFileProp.serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            var variablesProp = serializedObject.FindProperty("variables"); // This property references a visual scripting variables component.
            
            base.OnInspectorGUI();

            GUILayout.Space(15);

            if (variablesProp.objectReferenceValue && 
                variablesProp.objectReferenceValue is Variables v &&
                target &&
                target is MonoBehaviour m &&
                v.gameObject != m.gameObject)
            {
                RenderVariablesEditor(variablesProp);
            }
            else if (!variablesProp.objectReferenceValue && javascriptFileProp.objectReferenceValue)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUILayout.LabelField(new GUIContent("Variables", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image), EditorStyles.boldLabel); 
                EditorGUILayout.EndHorizontal(); 
                
                EditorGUILayout.HelpBox("This script does not have any variables. You can add variables to this script by creating a new Variables component.", MessageType.Info);
                if (GUILayout.Button("Create New Variables Component"))
                {
                    var popup = new GenericMenu();
                    popup.AddItem(new GUIContent("Add to this Object"), false, CreateNewVariablesComponent, false);
                    popup.AddItem(new GUIContent("Create New Child Object"), false, CreateNewVariablesComponent, true);
                    popup.ShowAsContext();
                }
            }

            if (variablesProp.objectReferenceValue)
            {
                var defaultVariables = GetScriptDefaultVariables();
                var variables = variablesProp.objectReferenceValue as Variables;
                if (variables)
                {
                    foreach (var variable in 
                             defaultVariables.Where(variable => !variables.declarations.IsDefined(variable.Key)))
                    {
                    }
                }
            }
        }
        
        private void CreateNewVariablesComponent(object createAsChild)
        {
            GameObject host;
            if (createAsChild is true)
            {
                host = new GameObject("Variables");
                host.transform.SetParent((target as MonoBehaviour)?.transform);
            }
            else
            {
                host = (target as MonoBehaviour)!.gameObject;
            }

            host.AddComponent<Variables>();
            var variablesProp = serializedObject.FindProperty("variables");
            variablesProp.objectReferenceValue = host.GetComponent<Variables>();
            variablesProp.serializedObject.ApplyModifiedProperties();
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
            var modifiedProperties = modifications
                .Where(mod => mod.target == source && !string.IsNullOrEmpty(mod.propertyPath))
                .ToArray();
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
        
        private Dictionary<string, (Type, object)> GetScriptDefaultVariables()
        {
            var result = new Dictionary<string, (Type, object)>();
            var script = (target as MetaverseScript)?.javascriptFile;
            if (script == null)
                return result;
            
            var scriptText = script.text;
            // This regex pattern matches all GetVar calls in the script.
            var matches = Regex.Matches(scriptText, @"GetVar\(""(?<name>[^""]+)"", ""(?<value>[^""]+)""\);");
            foreach (Match match in matches)
            {
                var n = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                if (float.TryParse(value, out var doubleValue))
                    result[n] = (typeof(float), doubleValue);
                else if (int.TryParse(value, out var intValue))
                    result[n] = (typeof(int), intValue);
                else if (bool.TryParse(value, out var boolValue))
                    result[n] = (typeof(bool), boolValue);
                else if (value == "null")
                    result[n] = (typeof(object), null);
                else if (value.StartsWith("\"") && value.EndsWith("\""))
                    result[n] = (typeof(string), value);
            }

            return result;
        } 
    }
}
