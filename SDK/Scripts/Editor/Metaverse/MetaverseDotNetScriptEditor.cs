using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MetaverseCloudEngine.Unity.Scripting.Components;
using TriInspectorMVCE;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using ReflectionAssembly = System.Reflection.Assembly;
using SerializedField = MetaverseCloudEngine.Unity.Scripting.Components.MetaverseDotNetScript.SerializedField;
using SerializedFieldKind = MetaverseCloudEngine.Unity.Scripting.Components.MetaverseDotNetScript.SerializedFieldKind;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomEditor(typeof(MetaverseDotNetScript), editorForChildClasses: true)]
    internal class MetaverseDotNetScriptEditor : TriMonoBehaviourEditor
    {
        private static readonly string[] EmptyTypeNames = Array.Empty<string>();
        private static readonly System.Collections.Generic.Dictionary<string, string[]> CachedTypes = new();
        private static readonly System.Collections.Generic.Dictionary<string, ReflectionAssembly> CachedEditorAssemblies = new();

        protected override void OnHeaderGUI() { }

        protected override bool ShouldHideOpenButton() => true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var assemblyProp = serializedObject.FindProperty("assemblyAsset");
            var classNameProp = serializedObject.FindProperty("className");

            EditorGUILayout.PropertyField(assemblyProp, new GUIContent("Assembly"));

            if (!assemblyProp.objectReferenceValue)
            {
                if (GUILayout.Button("Select Assembly"))
                {
                    var path = EditorUtility.OpenFilePanel("Select Assembly", Application.dataPath, "bytes");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var assetPath = path.Replace(Application.dataPath, "Assets");
                        assemblyProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            else
            {
                var assetPath = AssetDatabase.GetAssetPath(assemblyProp.objectReferenceValue);
                var isDllBytes = !string.IsNullOrEmpty(assetPath) &&
                                 assetPath.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(assetPath) && !isDllBytes)
                {
                    EditorGUILayout.HelpBox("The selected file is not a compiled C# assembly (.dll.bytes).", MessageType.Warning);
                }

                if (isDllBytes && assemblyProp.objectReferenceValue is TextAsset assemblyAsset)
                {
                    DrawSecurityStatus(assemblyAsset);
                }
            }

            // DrawScriptDragAndDropArea(assemblyProp, classNameProp);
            DrawClassSelection(assemblyProp, classNameProp);

            EditorGUILayout.Space();

            DrawSerializedScriptFields(assemblyProp, classNameProp);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            Editor.DrawPropertiesExcluding(serializedObject, "m_Script", "assemblyAsset", "className", "serializedFields");
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }

        private void DrawSecurityStatus(TextAsset assemblyAsset)
        {
            if (!assemblyAsset)
                return;

            // If security is globally disabled, surface that explicitly as a warning.
            if (!MetaverseDotNetScriptSecurity.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "Security validation is currently disabled for Metaverse .NET scripts.",
                    MessageType.Warning);
                return;
            }

            try
            {
                // Only show a message when validation fails; a successful validation is silent.
                if (!MetaverseDotNetScriptSecurity.ValidateAssemblyBytes(assemblyAsset.bytes, out var message))
                {
                    EditorGUILayout.HelpBox(
                        $"Security validation failed for this assembly: {message}",
                        MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox(
                    $"Security validation encountered an unexpected error: {ex.GetType().Name}: {ex.Message}",
                    MessageType.Error);
            }
        }

			private void DrawScriptDragAndDropArea(SerializedProperty assemblyProp, SerializedProperty classNameProp)
			{
				var rect = GUILayoutUtility.GetRect(
					new GUIContent("Drag C# script (.cs) here to auto-configure"),
					EditorStyles.helpBox,
					GUILayout.Height(40f),
					GUILayout.ExpandWidth(true));

				GUI.Box(rect, "Drag C# script (.cs) here to auto-configure", EditorStyles.helpBox);

				var evt = Event.current;
				if (!rect.Contains(evt.mousePosition))
					return;

				switch (evt.type)
				{
					case EventType.DragUpdated:
					case EventType.DragPerform:
						if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
							return;

						var hasCs = DragAndDrop.objectReferences
							.Select(o => AssetDatabase.GetAssetPath(o))
							.Any(p => !string.IsNullOrEmpty(p) && p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

						if (!hasCs)
							return;

						DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

						if (evt.type == EventType.DragPerform)
						{
							DragAndDrop.AcceptDrag();

							foreach (var obj in DragAndDrop.objectReferences)
							{
								var path = AssetDatabase.GetAssetPath(obj);
								if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
									continue;

								if (TryConfigureFromScriptPath(path, out var assemblyAsset, out var fullTypeName))
								{
									assemblyProp.objectReferenceValue = assemblyAsset;
									classNameProp.stringValue = fullTypeName;
									serializedObject.ApplyModifiedProperties();
									GUIUtility.ExitGUI();
								}
							}
						}

						evt.Use();
						break;
				}
			}

			internal static bool TryConfigureFromScriptPath(string scriptAssetPath, out TextAsset assemblyAsset, out string fullTypeName)
			{
				assemblyAsset = null;
				fullTypeName = null;

				if (string.IsNullOrEmpty(scriptAssetPath))
					return false;

				if (!scriptAssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
					return false;

				if (!scriptAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
				{
					Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Script '{scriptAssetPath}' is not under the Assets folder. Only assets under 'Assets/' are supported.");
					return false;
				}

				string assemblyName;
				try
				{
					assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptAssetPath);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to resolve assembly for script '{scriptAssetPath}': {ex}");
					return false;
				}

				if (string.IsNullOrEmpty(assemblyName))
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Could not determine assembly for script '{scriptAssetPath}'. Make sure it is included in an Assembly Definition.");
					return false;
				}

				var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
				if (string.IsNullOrEmpty(asmdefPath))
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Assembly '{assemblyName}' is not backed by an Assembly Definition asset. .NET scripts require an asmdef.");
					return false;
				}

				if (!MetaverseDotNetAsmdefBuilder.TryEnsureAssemblyAsset(asmdefPath, out assemblyAsset))
				{
					// TryEnsureAssemblyAsset already logged the reason.
					return false;
				}

				fullTypeName = TryExtractDotNetScriptTypeNameFromSource(scriptAssetPath);
				if (string.IsNullOrEmpty(fullTypeName))
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Could not find a class deriving from MetaverseDotNetScriptBase or implementing IMetaverseDotNetScript in '{scriptAssetPath}'.");
					assemblyAsset = null;
					return false;
				}

				return true;
			}

			internal static string TryExtractDotNetScriptTypeNameFromSource(string scriptAssetPath)
			{
				try
				{
					var absolutePath = Path.GetFullPath(scriptAssetPath);
					if (!File.Exists(absolutePath))
						return null;

					var source = File.ReadAllText(absolutePath);

					string namespaceName = null;
					var namespaceMatch = Regex.Match(source, @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)");
					if (namespaceMatch.Success)
						namespaceName = namespaceMatch.Groups[1].Value.Trim();

					var classMatches = Regex.Matches(source, @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:\:\s*([^\r\n\{]+))?");
					foreach (Match match in classMatches)
					{
						if (!match.Success)
							continue;

						var name = match.Groups[1].Value;
						var bases = match.Groups[2].Value;

						if (!string.IsNullOrEmpty(bases) &&
							(bases.Contains("MetaverseDotNetScriptBase") || bases.Contains("IMetaverseDotNetScript")))
						{
							return !string.IsNullOrEmpty(namespaceName)
								? $"{namespaceName}.{name}"
								: name;
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to parse script file '{scriptAssetPath}' when determining class name: {ex}");
				}

				return null;
			}




        private static GUIContent _editClassButtonContent;

        private static GUIContent EditClassButtonContent
        {
            get
            {
                if (_editClassButtonContent != null)
                    return _editClassButtonContent;

                // Try a couple of known pencil/edit icons, fall back to a small text button.
                var iconNames = new[] { "editicon.sml", "d_editicon.sml" };
                foreach (var name in iconNames)
                {
                    var content = EditorGUIUtility.IconContent(name);
                    if (content != null && content.image != null)
                    {
                        _editClassButtonContent = new GUIContent(content.image, "Open script in editor");
                        return _editClassButtonContent;
                    }
                }

                _editClassButtonContent = new GUIContent("E", "Open script in editor");
                return _editClassButtonContent;
            }
        }

        private static void OpenScriptForClass(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                Debug.LogWarning("[METAVERSE_DOTNET_SCRIPT] Cannot open script because no class is selected.");
                return;
            }

            try
            {
                var scripts = MonoImporter.GetAllRuntimeMonoScripts();
                if (scripts == null || scripts.Length == 0)
                {
                    Debug.LogWarning("[METAVERSE_DOTNET_SCRIPT] No runtime scripts found when trying to open class '" + fullTypeName + "'.");
                    return;
                }

                MonoScript matchingScript = null;
                foreach (var script in scripts)
                {
                    if (!script)
                        continue;

                    var type = script.GetClass();
                    if (type != null && type.FullName == fullTypeName)
                    {
                        matchingScript = script;
                        break;
                    }
                }

                if (matchingScript != null)
                {
                    AssetDatabase.OpenAsset(matchingScript);
                }
                else
                {
                    Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Could not locate source file for class '{fullTypeName}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to open script for class '{fullTypeName}': {ex}");
            }
        }

        private void DrawClassSelection(SerializedProperty assemblyProp, SerializedProperty classNameProp)
        {
            using (new EditorGUI.DisabledScope(!assemblyProp.objectReferenceValue))
            {
                var labels = GetScriptTypeNames(assemblyProp.objectReferenceValue as TextAsset);
                if (labels.Length > 0)
                {
                    var currentIndex = Array.IndexOf(labels, classNameProp.stringValue);
                    if (currentIndex < 0) currentIndex = 0;

                    var controlRect = EditorGUILayout.GetControlRect();
                    controlRect = EditorGUI.PrefixLabel(controlRect, new GUIContent("Class"));

                    var buttonWidth = EditorGUIUtility.singleLineHeight + 4f;
                    var popupRect = new Rect(controlRect.x, controlRect.y, controlRect.width - buttonWidth - 2f, controlRect.height);
                    var buttonRect = new Rect(popupRect.xMax + 2f, controlRect.y, buttonWidth, controlRect.height);

                    EditorGUI.BeginChangeCheck();
                    var newIndex = EditorGUI.Popup(popupRect, currentIndex, labels);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (newIndex >= 0 && newIndex < labels.Length)
                        {
                            classNameProp.stringValue = labels[newIndex];
                            serializedObject.ApplyModifiedProperties();
                        }
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(classNameProp.stringValue)))
                    {
                        if (GUI.Button(buttonRect, EditClassButtonContent, EditorStyles.miniButton))
                        {
                            OpenScriptForClass(classNameProp.stringValue);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No types implementing IMetaverseDotNetScript were found in the selected assembly.", MessageType.Info);
                    classNameProp.stringValue = EditorGUILayout.TextField("Class", classNameProp.stringValue);
                }
            }
        }

        private static bool TryLoadAssemblyForInspector(TextAsset assemblyAsset, out ReflectionAssembly assembly)
        {
            assembly = null;

            if (!assemblyAsset)
                return false;

            var path = AssetDatabase.GetAssetPath(assemblyAsset);
            if (string.IsNullOrEmpty(path))
                return false;

            if (CachedEditorAssemblies.TryGetValue(path, out assembly) && assembly != null)
                return true;

            try
            {
                var bytes = assemblyAsset.bytes;
                if (bytes == null || bytes.Length == 0)
                    return false;

                assembly = ReflectionAssembly.Load(bytes);
                CachedEditorAssemblies[path] = assembly;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to load assembly for inspector from '{assemblyAsset.name}': {ex}");
                CachedEditorAssemblies[path] = null;
                return false;
            }
        }

        private static bool IsUnitySerializableField(FieldInfo field)
        {
            if (field == null)
                return false;

            if (field.IsStatic)
                return false;

            if (field.IsInitOnly || field.IsLiteral)
                return false;

            var hasHideInInspector = field.GetCustomAttributes(typeof(HideInInspector), inherit: true).Any();
            if (hasHideInInspector)
                return false;

            var isPublic = field.IsPublic;
            var isSerializeField = field.GetCustomAttributes(typeof(SerializeField), inherit: true).Any();

            if (!isPublic && !isSerializeField)
                return false;

            return IsUnitySerializableType(field.FieldType);
        }

        private static bool IsUnitySerializableType(Type type)
        {
            if (type == null)
                return false;

            if (type.IsEnum)
                return true;

            if (type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(string))
                return true;

            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Color))
                return true;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;

            return false;
        }

        private static SerializedFieldKind GetSerializedFieldKindForType(Type fieldType)
        {
            if (fieldType == typeof(bool)) return SerializedFieldKind.Bool;
            if (fieldType == typeof(int) || fieldType.IsEnum) return SerializedFieldKind.Int;
            if (fieldType == typeof(float)) return SerializedFieldKind.Float;
            if (fieldType == typeof(string)) return SerializedFieldKind.String;
            if (fieldType == typeof(Vector2)) return SerializedFieldKind.Vector2;
            if (fieldType == typeof(Vector3)) return SerializedFieldKind.Vector3;
            if (fieldType == typeof(Color)) return SerializedFieldKind.Color;
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) return SerializedFieldKind.ObjectReference;

            return SerializedFieldKind.String;
        }

        private static object GetDefaultValue(Type t)
        {
            if (t == null)
                return null;

            if (t.IsValueType)
                return Activator.CreateInstance(t);

            return null;
        }

        private void DrawSerializedScriptFields(SerializedProperty assemblyProp, SerializedProperty classNameProp)
        {
            if (!assemblyProp.objectReferenceValue)
                return;

            var assemblyAsset = assemblyProp.objectReferenceValue as TextAsset;
            if (!assemblyAsset)
                return;

            var className = classNameProp.stringValue;
            if (string.IsNullOrEmpty(className))
                return;

            if (!TryLoadAssemblyForInspector(assemblyAsset, out var assembly) || assembly == null)
                return;

            Type scriptType = null;
            try
            {
                scriptType = assembly.GetType(className, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                // ignored
            }

            if (scriptType == null)
                return;

            var targetScript = (MetaverseDotNetScript)target;
            var fieldsList = targetScript.SerializedFields;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var typeCursor = scriptType;

            using (new EditorGUI.IndentLevelScope())
            {
                while (typeCursor != null && typeCursor != typeof(object))
                {
                    var fields = typeCursor.GetFields(flags);
                    foreach (var field in fields)
                    {
                        if (!IsUnitySerializableField(field))
                            continue;

                        var headerAttrs = field.GetCustomAttributes(typeof(HeaderAttribute), inherit: true).Cast<HeaderAttribute>().ToArray();
                        foreach (var header in headerAttrs)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
                        }

                        var spaceAttrs = field.GetCustomAttributes(typeof(SpaceAttribute), inherit: true).Cast<SpaceAttribute>().ToArray();
                        foreach (var space in spaceAttrs)
                        {
                            if (space.height > 0f)
                                EditorGUILayout.Space(space.height / EditorGUIUtility.singleLineHeight);
                            else
                                EditorGUILayout.Space();
                        }

                        var tooltipAttr = field.GetCustomAttributes(typeof(TooltipAttribute), inherit: true).Cast<TooltipAttribute>().FirstOrDefault();
                        var label = tooltipAttr != null
                            ? new GUIContent(ObjectNames.NicifyVariableName(field.Name), tooltipAttr.tooltip)
                            : new GUIContent(ObjectNames.NicifyVariableName(field.Name));

                        var kind = GetSerializedFieldKindForType(field.FieldType);

                        var sf = fieldsList.FirstOrDefault(f => f.fieldName == field.Name && f.declaringTypeName == field.DeclaringType.FullName);
                        if (sf == null)
                        {
                            sf = new SerializedField
                            {
                                fieldName = field.Name,
                                declaringTypeName = field.DeclaringType.FullName,
                                kind = kind,
                                hasOverride = false
                            };
                            fieldsList.Add(sf);
                        }

                        object currentValue = null;

                        if (!sf.hasOverride)
                        {
                            currentValue = GetDefaultValue(field.FieldType);
                            if (field.FieldType.IsEnum)
                            {
                                try
                                {
                                    var values = Enum.GetValues(field.FieldType);
                                    currentValue = values.Length > 0 ? values.GetValue(0) : null;
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                        else
                        {
                            switch (sf.kind)
                            {
                                case SerializedFieldKind.Bool:
                                    currentValue = sf.boolValue;
                                    break;
                                case SerializedFieldKind.Int:
                                    currentValue = sf.intValue;
                                    break;
                                case SerializedFieldKind.Float:
                                    currentValue = sf.floatValue;
                                    break;
                                case SerializedFieldKind.String:
                                    currentValue = sf.stringValue;
                                    break;
                                case SerializedFieldKind.Vector2:
                                    currentValue = sf.vector2Value;
                                    break;
                                case SerializedFieldKind.Vector3:
                                    currentValue = sf.vector3Value;
                                    break;
                                case SerializedFieldKind.Color:
                                    currentValue = sf.colorValue;
                                    break;
                                case SerializedFieldKind.ObjectReference:
                                    currentValue = sf.objectReference;
                                    break;
                            }
                        }

                        EditorGUI.BeginChangeCheck();

                        object newValue = null;

                        if (kind == SerializedFieldKind.Bool)
                        {
                            var v = currentValue is bool b ? b : default;
                            newValue = EditorGUILayout.Toggle(label, v);
                        }
                        else if (kind == SerializedFieldKind.Int)
                        {
                            if (field.FieldType.IsEnum)
                            {
                                var enumValue = currentValue ?? Enum.GetValues(field.FieldType).GetValue(0);
                                var enumObj = EditorGUILayout.EnumPopup(label, (Enum)enumValue);
                                newValue = enumObj;
                            }
                            else
                            {
                                var v = currentValue is int i ? i : default;
                                newValue = EditorGUILayout.IntField(label, v);
                            }
                        }
                        else if (kind == SerializedFieldKind.Float)
                        {
                            var v = currentValue is float f ? f : default;
                            newValue = EditorGUILayout.FloatField(label, v);
                        }
                        else if (kind == SerializedFieldKind.String)
                        {
                            var v = currentValue as string ?? string.Empty;
                            newValue = EditorGUILayout.TextField(label, v);
                        }
                        else if (kind == SerializedFieldKind.Vector2)
                        {
                            var v = currentValue is Vector2 v2 ? v2 : default;
                            newValue = EditorGUILayout.Vector2Field(label, v);
                        }
                        else if (kind == SerializedFieldKind.Vector3)
                        {
                            var v = currentValue is Vector3 v3 ? v3 : default;
                            newValue = EditorGUILayout.Vector3Field(label.text, v);
                        }
                        else if (kind == SerializedFieldKind.Color)
                        {
                            var v = currentValue is Color c ? c : default;
                            newValue = EditorGUILayout.ColorField(label, v);
                        }
                        else if (kind == SerializedFieldKind.ObjectReference)
                        {
                            var v = currentValue as UnityEngine.Object;
                            newValue = EditorGUILayout.ObjectField(label, v, field.FieldType, allowSceneObjects: true);
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            sf.kind = kind;
                            sf.hasOverride = true;

                            if (kind == SerializedFieldKind.Bool)
                            {
                                sf.boolValue = newValue is bool b ? b : sf.boolValue;
                            }
                            else if (kind == SerializedFieldKind.Int)
                            {
                                if (field.FieldType.IsEnum && newValue is Enum enumObj)
                                {
                                    sf.intValue = Convert.ToInt32(enumObj);
                                }
                                else
                                {
                                    sf.intValue = newValue is int i ? i : sf.intValue;
                                }
                            }
                            else if (kind == SerializedFieldKind.Float)
                            {
                                sf.floatValue = newValue is float f ? f : sf.floatValue;
                            }
                            else if (kind == SerializedFieldKind.String)
                            {
                                sf.stringValue = newValue as string ?? sf.stringValue;
                            }
                            else if (kind == SerializedFieldKind.Vector2)
                            {
                                sf.vector2Value = newValue is Vector2 v2 ? v2 : sf.vector2Value;
                            }
                            else if (kind == SerializedFieldKind.Vector3)
                            {
                                sf.vector3Value = newValue is Vector3 v3 ? v3 : sf.vector3Value;
                            }
                            else if (kind == SerializedFieldKind.Color)
                            {
                                sf.colorValue = newValue is Color c ? c : sf.colorValue;
                            }
                            else if (kind == SerializedFieldKind.ObjectReference)
                            {
                                sf.objectReference = newValue as UnityEngine.Object;
                            }

                            EditorUtility.SetDirty(targetScript);
                        }
                    }

                    typeCursor = typeCursor.BaseType;
                }
            }
        }


        private static string[] GetScriptTypeNames(TextAsset assemblyAsset)
        {
            if (!assemblyAsset)
                return EmptyTypeNames;

            var path = AssetDatabase.GetAssetPath(assemblyAsset);
            if (string.IsNullOrEmpty(path))
                return EmptyTypeNames;

            if (CachedTypes.TryGetValue(path, out var cached))
                return cached;

            try
            {
                if (!TryLoadAssemblyForInspector(assemblyAsset, out var assembly) || assembly == null)
                    return EmptyTypeNames;

                var interfaceFullName = typeof(IMetaverseDotNetScript).FullName;
                var baseFullName = typeof(MetaverseDotNetScriptBase).FullName;

                Type[] allTypes;
                try
                {
                    allTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    allTypes = rtle.Types.Where(t => t != null).ToArray();
                    Debug.LogWarning($"[METAVERSE_DOTNET_SCRIPT] Partial type load when inspecting assembly '{assemblyAsset.name}': {rtle}");
                }

                var types = allTypes
                    .Where(t => t.IsClass && !t.IsAbstract && ImplementsDotNetScriptContract(t, interfaceFullName, baseFullName))
                    .Select(t => t.FullName)
                    .OrderBy(n => n)
                    .ToArray();

                CachedTypes[path] = types;
                return types;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to inspect assembly '{assemblyAsset.name}': {ex}");
                CachedTypes[path] = EmptyTypeNames;
                return EmptyTypeNames;
            }
        }

        private static bool ImplementsDotNetScriptContract(Type type, string interfaceFullName, string baseFullName)
        {
            if (type == null)
                return false;

            // Fast path for the normal case where runtime types share the same load context
            try
            {
                if (typeof(IMetaverseDotNetScript).IsAssignableFrom(type))
                    return true;
            }
            catch
            {
                // Ignore and fall back to name-based checks below.
            }

            // Fallback 1: interface match by full name (avoids load-context identity issues)
            try
            {
                if (type.GetInterfaces().Any(i => i.FullName == interfaceFullName))
                    return true;
            }
            catch
            {
                // ignored
            }

            // Fallback 2: base-type chain match by full name (for MetaverseDotNetScriptBase subclasses)
            try
            {
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    if (baseType.FullName == baseFullName)
                        return true;

                    baseType = baseType.BaseType;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }
}

