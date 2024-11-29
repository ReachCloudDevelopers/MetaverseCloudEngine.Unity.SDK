using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private Editor _variablesEditor;

        private Dictionary<string, (Type, object)> _scriptDefaultVariables = new();
        private bool _isBuildingVariables;
        private static readonly ConcurrentDictionary<string, Type> CachedTypeData = new();

        public override void OnInspectorGUI()
        {
            var javascriptFileProp = serializedObject.FindProperty("javascriptFile");

            MetaverseEditorUtils.Header(javascriptFileProp.objectReferenceValue != null
                ? javascriptFileProp.objectReferenceValue.name
                : "(No Script)", false);

            if (!javascriptFileProp.objectReferenceValue)
            {
                if (GUILayout.Button("Select Script"))
                {
                    var path = EditorUtility.OpenFilePanel("Select Script", Application.dataPath, "js");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var assetPath = path.Replace(Application.dataPath, "Assets");
                        javascriptFileProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                        javascriptFileProp.serializedObject.ApplyModifiedProperties();
                    }
                }

                if (GUILayout.Button("Create New Script"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Create New Script", "NewScript", "js",
                        "Create a new script file");
                    if (!string.IsNullOrEmpty(path))
                    {
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
            else if (!AssetDatabase.GetAssetPath(javascriptFileProp.objectReferenceValue).EndsWith(".js"))
            {
                EditorGUILayout.HelpBox(
                    "The selected file is not a JavaScript file. Please select a valid JavaScript file.",
                    MessageType.Error);
                if (GUILayout.Button("Select New Script"))
                {
                    var path = EditorUtility.OpenFilePanel("Select Script", Application.dataPath, "js");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var assetPath = path.Replace(Application.dataPath, "Assets");
                        javascriptFileProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                        javascriptFileProp.serializedObject.ApplyModifiedProperties();
                    }
                }

                return;
            }
            else
            {
                if (GUILayout.Button("Open Script"))
                {
                    AssetDatabase.OpenAsset(javascriptFileProp.objectReferenceValue);
                }

                if (GUILayout.Button("Replace Script"))
                {
                    var path = EditorUtility.OpenFilePanel("Select Script", Application.dataPath, "js");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var assetPath = path.Replace(Application.dataPath, "Assets");
                        javascriptFileProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                        javascriptFileProp.serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Delete Script"))
                {
                    if (EditorUtility.DisplayDialog("Delete Script", "Are you sure you want to delete the script?",
                            "Yes", "No"))
                    {
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(javascriptFileProp.objectReferenceValue));
                        javascriptFileProp.objectReferenceValue = null;
                        javascriptFileProp.serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            var variablesProp =
                serializedObject
                    .FindProperty("variables"); // This property references a visual scripting variables component.

            base.OnInspectorGUI();

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
                EditorGUILayout.LabelField(
                    new GUIContent("Variables", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image),
                    EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "This script does not have any variables. You can add variables to this script by creating a new Variables component.",
                    MessageType.Info);
                if (GUILayout.Button("Create New Variables Component"))
                {
                    var popup = new GenericMenu();
                    popup.AddItem(new GUIContent("Add to this Object"), false, CreateNewVariablesComponent, false);
                    popup.AddItem(new GUIContent("Create New Child Object"), false, CreateNewVariablesComponent, true);
                    popup.ShowAsContext();
                }
            }

            if (!variablesProp.objectReferenceValue) 
                return;

            if (_isBuildingVariables)
            {
                EditorGUILayout.HelpBox("Loading variables...", MessageType.Info);
                return;
            }
            
            var defaultVariables = GetScriptDefaultVariablesAsync();
            var variables = variablesProp.objectReferenceValue as Variables;
            if (!variables) 
                return;
            
            var varsToBeAdded = defaultVariables
                .Where(variable => !variables.declarations.IsDefined(variable.Key)).ToArray();
            if (varsToBeAdded.Length > 0)
            {
                EditorGUILayout.HelpBox(
                    "The following variables are defined in the script but not in the Variables component. You can add them by clicking the 'Add Variable' button.",
                    MessageType.Info);
                var addAll = GUILayout.Button("Add All Variables");
                foreach (var variable in varsToBeAdded)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(
                        "var " + variable.Key + " : " + variable.Value.Item1.Name + " = " +
                        (variable.Value.Item2?.ToString() ?? "default(null)") + ";", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "This variable is defined in the script but not in the Variables component.",
                        EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Add Variable") || addAll)
                    {
                        variables.declarations.Set(variable.Key, variable.Value.Item2);
                        var declaration = variables.declarations.GetDeclaration(variable.Key);
                        declaration.typeHandle =
                            new SerializableType(variable.Value.Item1.AssemblyQualifiedName);
                        variablesProp.serializedObject.ApplyModifiedProperties();
                    }

                    EditorGUILayout.EndVertical();
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
            EditorGUILayout.LabelField(
                new GUIContent("Variables", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image),
                EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Make it so that when we start dragging the variables header, it beings a drag operation
            // for the variables component itself.
            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) &&
                Event.current.type == EventType.MouseDown)
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

        private Dictionary<string, (Type, object)> GetScriptDefaultVariablesAsync()
        {
            if (_scriptDefaultVariables != null)
                return _scriptDefaultVariables;

            var result = new Dictionary<string, (Type, object)>();
            var script = (target as MetaverseScript)?.javascriptFile;
            if (script == null)
                return _scriptDefaultVariables = result;

            var scriptText = script.text;
            _scriptDefaultVariables = new Dictionary<string, (Type, object)>();
            _isBuildingVariables = true;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var matches = Regex.Matches(scriptText,
                    @".*?(?:(?:DefineVar\s*\(\s*(?<name>\""\S+?\"")\s*\,\s*(?<value>\S+?)\)\s*\;*){1})|(?:(?:DefineTypedVar\s*\(\s*(?<name>\""\S+?\"")\s*\,\s*(?<type>\""\S+?\"")\s*\,\s*(?<value>\S+?)\)\s*\;*){1})");
                foreach (Match match in matches)
                {
                    // Make sure it is outside a comment.
                    var val = match.Value;
                    // Make sure it's not inside any scope.
                    var index = scriptText.IndexOf(val, StringComparison.Ordinal);
                    var scriptBefore = scriptText[..index];
                    var openBraces = scriptBefore.Count(c => c == '{');
                    var closeBraces = scriptBefore.Count(c => c == '}');
                    if (openBraces != closeBraces)
                        continue;

                    var variableName = match.Groups["name"].Value.Replace("\"", "");
                    if (string.IsNullOrEmpty(variableName))
                        continue;
                    var rawValue = match.Groups["value"].Value;
                    var value = rawValue.Replace("\"", "");
                    if (string.IsNullOrEmpty(value))
                        continue;
                    var type = match.Groups["type"].Value.Replace("\"", "");
                    if (!string.IsNullOrEmpty(type))
                    {
                        // Find first type with name matching the type string.
                        var t = GetCachedType(type);
                        if (t == null)
                            continue;

                        // If the type contains a "TryParse" method, let's try to parse the
                        // default value as that type.
                        var tryParse = t.GetMethod("TryParse", new[] { typeof(string), t.MakeByRefType() });
                        if (tryParse != null)
                        {
                            var parameters = new object[] { value, null };
                            if ((bool)tryParse.Invoke(null, parameters))
                                result[variableName] = (t, parameters[1]);
                        }
                        else
                            result[variableName] = (t, null);
                        continue;
                    }

                    if (float.TryParse(value, out var doubleValue))
                        result[variableName] = (typeof(float), doubleValue);
                    else if (int.TryParse(value, out var intValue))
                        result[variableName] = (typeof(int), intValue);
                    else if (bool.TryParse(value, out var boolValue))
                        result[variableName] = (typeof(bool), boolValue);
                    else if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                        result[variableName] = (typeof(string), value);
                    else if (value == "null")
                        result[variableName] = (typeof(object), null);
                }

                _scriptDefaultVariables = result;
            });
            
            return _scriptDefaultVariables;
        }

        private static Type GetCachedType(string type)
        {
            if (CachedTypeData.TryGetValue(type, out var cachedType))
                return cachedType;
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Namespace + "." + t.Name == type);
            CachedTypeData[type] = t;
            return t;
        }
    }
}
