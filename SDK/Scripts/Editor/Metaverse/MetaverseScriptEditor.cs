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
                    var varsToBeAdded = defaultVariables.Where(variable => !variables.declarations.IsDefined(variable.Key)).ToArray();
                    if (varsToBeAdded.Length > 0)
                    {
                        EditorGUILayout.HelpBox("The following variables are defined in the script but not in the Variables component. You can add them by clicking the 'Add Variable' button.", MessageType.Info);
                        foreach (var variable in varsToBeAdded)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                            EditorGUILayout.LabelField("var " + variable.Key + " : " + variable.Value.Item1.Name.ToLower() + " = " + (variable.Value.Item2?.ToString() ?? "default(null)") + ";", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField("This variable is defined in the script but not in the Variables component.", EditorStyles.wordWrappedLabel);
                            if (GUILayout.Button("Add Variable"))
                            {
                                variables.declarations.Set(variable.Key, variable.Value.Item2);
                                var declaration = variables.declarations.GetDeclaration(variable.Key);
                                declaration.typeHandle = new SerializableType(variable.Value.Item1.AssemblyQualifiedName);
                                variablesProp.serializedObject.ApplyModifiedProperties();
                            }
                            EditorGUILayout.EndVertical();
                        }
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
            // Matches
            // = GetVar("name", "value"); // With quotes
            // or 
            // = GetVar("name", 0.0); // No quotes
            // or
            // = GetVar("name", true);
            // or
            // = GetVar("name", null);
            // etc.
            var matches = Regex.Matches(scriptText, @".*?GetVar\(""(?<name>.+?)""\s*,\s*(?<value>.+?)\);"); 
            foreach (Match match in matches)
            {
                // Make sure it is outside a comment.
                var val = match.Value;
                if (val.Trim().StartsWith("//") || 
                    val.Trim().StartsWith("/*") ||
                    (val.Split('=').Length > 2 && val.Split('=')[0].Trim().EndsWith("//") ||
                     val.Split('=')[0].Trim().EndsWith("/*") ||
                     val.Split('=')[1].Trim().StartsWith("//") ||
                     val.Split('=')[1].Trim().StartsWith("/*")))
                    continue;

                // Make sure it's not inside any scope.
                var index = scriptText.IndexOf(val, StringComparison.Ordinal);
                var scriptBefore = scriptText.Substring(0, index);
                var scriptAfter = scriptText.Substring(index + val.Length);
                var openBraces = scriptBefore.Count(c => c == '{');
                var closeBraces = scriptBefore.Count(c => c == '}');
                if (openBraces != closeBraces)
                    continue;
                
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
