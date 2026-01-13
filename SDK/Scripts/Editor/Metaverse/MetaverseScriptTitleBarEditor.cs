using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using UnityEngine;
using MetaverseCloudEngine.Unity.Scripting.Components;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;


namespace MetaverseCloudEngine.Unity.Editors
{
    [InitializeOnLoad]
    internal static class MetaverseScriptTitleBarEditor
    {
		private const string ExpandedPrefsPrefix = "MetaverseCloudEngine.MetaverseScript.expanded.";
		private const string InternalIdFieldName = "_internalId";

        static MetaverseScriptTitleBarEditor()
		{
			ComponentTitlebarGUI.OnTitlebarGUI += OnTitlebarGUI;
        }

        private static void OnTitlebarGUI(Rect rect, Object @object)
		{
			if (@object is not MetaverseScript ms) return;

			EnsureHasUniqueInternalId(ms);

			var serializedObject = new SerializedObject(ms);
            var javascriptFileProp = serializedObject.FindProperty("javascriptFile");

            // Fake header showing the assigned JS file name (or (No Script))
            var jsAsset = javascriptFileProp.objectReferenceValue as TextAsset;
            var displayTitle = jsAsset ? ObjectNames.NicifyVariableName(jsAsset.name) + " (Script)" : "(No Script)";
			var fakeHeaderRect = new Rect(0, 0, rect.width, rect.height);

            // Draw header background
			GUI.Box(fakeHeaderRect, GUIContent.none, EditorStyles.toolbar);

            // Areas inside header: foldout and enabled toggle (do not intercept), title elsewhere
            var foldRect = new Rect(fakeHeaderRect.x + 4f, fakeHeaderRect.y, 16f, fakeHeaderRect.height - 4f);
            var iconRect = new Rect(foldRect.x + 16f, fakeHeaderRect.y, fakeHeaderRect.height - 6f, fakeHeaderRect.height - 2f);
            GUI.Label(iconRect, EditorGUIUtility.GetIconForObject(ms));
            var toggleRect = new Rect(foldRect.xMax + 20f, fakeHeaderRect.y, 18f, fakeHeaderRect.height - 4f);
			var titleRect = new Rect(toggleRect.xMax + 2f, fakeHeaderRect.y, fakeHeaderRect.width - (toggleRect.xMax - fakeHeaderRect.x) - 8f, fakeHeaderRect.height);
            
			var prefsKey = ExpandedPrefsPrefix + ms.InternalId;
			var isExpanded = EditorPrefs.GetBool(prefsKey, true);
			var newExpanded = EditorGUI.Foldout(foldRect, isExpanded, GUIContent.none, true);
			if (newExpanded != isExpanded)
			{
				EditorPrefs.SetBool(prefsKey, newExpanded);
				InternalEditorUtility.SetIsInspectorExpanded(ms, newExpanded);
			}
			else
			{
				// Keep Unity's internal expanded state in sync with our per-component state.
				if (InternalEditorUtility.GetIsInspectorExpanded(ms) != isExpanded)
					InternalEditorUtility.SetIsInspectorExpanded(ms, isExpanded);
			}

            // Enabled toggle (uses serialized m_Enabled)
            var enabledProp = serializedObject.FindProperty("m_Enabled");
            if (enabledProp != null)
            {
                bool newEnabled = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
                if (newEnabled != enabledProp.boolValue)
                {
                    enabledProp.boolValue = newEnabled;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            // Title label
            GUI.Label(titleRect, displayTitle, EditorStyles.boldLabel);

            // Add three icons on the right side
            var iconSize = titleRect.height - 1;
            var rightMargin = 1f;
            var totalIconWidth = iconSize * 3;
            var startX = fakeHeaderRect.xMax - totalIconWidth - rightMargin;
            
            // Question mark icon (help)
            var helpIconRect = new Rect(startX, fakeHeaderRect.y, iconSize, iconSize);
            GUI.Label(helpIconRect, EditorGUIUtility.IconContent("_Help").image);
            
            // Preset/context icon
            var presetIconRect = new Rect(startX + iconSize - 1, fakeHeaderRect.y, iconSize, iconSize);
            GUI.Label(presetIconRect, EditorGUIUtility.IconContent("Preset.Context").image);
            
            // Menu icon
            var menuIconRect = new Rect(startX + (iconSize - 1) * 2, fakeHeaderRect.y, iconSize, iconSize);
            GUI.Label(menuIconRect, EditorGUIUtility.IconContent("_Menu").image);
        }

		private static void EnsureHasUniqueInternalId(MetaverseScript ms)
		{
			if (!ms)
				return;

			// Ensure the ID exists and is unique among MetaverseScript components on the same GameObject.
			var id = ms.InternalId;
			if (string.IsNullOrEmpty(id))
				id = null;

			var scripts = ms.gameObject ? ms.gameObject.GetComponents<MetaverseScript>() : Array.Empty<MetaverseScript>();
			var duplicateCount = 0;
			for (var i = 0; i < scripts.Length; i++)
			{
				var other = scripts[i];
				if (!other || other == ms)
					continue;
				if (!string.IsNullOrEmpty(other.InternalId) && other.InternalId == id)
					duplicateCount++;
			}

			if (!string.IsNullOrEmpty(id) && duplicateCount == 0)
				return;

			Undo.RecordObject(ms, "Assign MetaverseScript ID");
			var so = new SerializedObject(ms);
			var idProp = so.FindProperty(InternalIdFieldName);
			if (idProp != null)
			{
				idProp.stringValue = Guid.NewGuid().ToString("N");
				so.ApplyModifiedProperties();
			}
			else
			{
				// Fallback: touch the property getter to generate an ID, then mark dirty.
				_ = ms.InternalId;
				EditorUtility.SetDirty(ms);
			}
		}
    }

	public static class ComponentTitlebarGUI
	{
		public static Action<Rect, Object> OnTitlebarGUI;

#if UNITY_EDITOR
		private static ProfilerMarker _profileMarker = new ProfilerMarker($"{nameof(ComponentTitlebarGUI)}.{nameof(OnUpdate)}");
		private static ProfilerMarker _profileMarker2 = new ProfilerMarker($"{nameof(ComponentTitlebarGUI)}.OnTitlebarGUI");
		private static Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
		private static Type type2 = typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
		private static Type type3 = typeof(EditorWindow).Assembly.GetType("UnityEditor.UIElements.EditorElement");
		private static FieldInfo field = type2.GetField("m_EditorsElement", BindingFlags.NonPublic | BindingFlags.Instance);
		private static FieldInfo field2 = type3.GetField("m_Header", BindingFlags.NonPublic | BindingFlags.Instance);
		private static FieldInfo field3 = type3.GetField("m_EditorTarget", BindingFlags.NonPublic | BindingFlags.Instance);
		private static VisualElement m_EditorsElement;
		private static VisualElement editorsElement => m_EditorsElement ??= GetEditorVisualElement();
		private static Dictionary<VisualElement, Action> _callbacks = new();

		private static VisualElement GetEditorVisualElement()
		{
			EditorWindow window = EditorWindow.GetWindow(type);
			if (window)
			{
				return field.GetValue(window) as VisualElement;
			}

			return null;
		}

		[InitializeOnLoadMethod]
		public static void Init()
		{
			EditorApplication.update -= OnUpdate;
			EditorApplication.update += OnUpdate;
			Selection.selectionChanged -= OnSelectionChanged;
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnSelectionChanged() => _callbacks.Clear();

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			_callbacks.Clear();
		}

		private static void OnUpdate()
		{
			using (_profileMarker.Auto())
			{
				VisualElement inspectorRoot = editorsElement;
				if (inspectorRoot == null) return;

				var foundAll = editorsElement.Children();
				foreach (VisualElement element in foundAll)
				{
					if (element.GetType() != type3) continue;

					var localTarget = field3.GetValue(element) as Object;
					if (localTarget)
					{
						IMGUIContainer value2 = field2.GetValue(element) as IMGUIContainer;
						Action callback = null;
						if (_callbacks.TryGetValue(element, out var found))
						{
							callback = found;
						}
						else
						{
							callback = _callbacks[element] = MyLocalCallback;
						}

						if (value2 != null)
						{
							value2.onGUIHandler -= callback;
							value2.onGUIHandler += callback;
						}

						void MyLocalCallback()
						{
							using (_profileMarker2.Auto())
							{
								try
								{
									var width = value2.contentRect.width > 1f ? value2.contentRect.width : value2.layout.width;
									var height = value2.contentRect.height > 1f ? value2.contentRect.height : value2.layout.height;
									var headerRect = new Rect(0, 0, width, height);
									OnTitlebarGUI?.Invoke(headerRect, localTarget);
								}
								catch (Exception e)
								{
									Debug.LogException(e);
								}
							}
						}
					}
				}
			}
		}

		//private static GUIContent content = EditorGUIUtility.IconContent("console.erroricon.sml");
		// [InitializeOnLoadMethod]
		// public static void InitTest()
		// {
		// 	ComponentTitlebarGUI.OnTitlebarGUI -= TestGUI;
		// 	ComponentTitlebarGUI.OnTitlebarGUI += TestGUI;
		// }
		//
		// private static void TestGUI(Rect rect, Object target)
		// {
		// 	if (target is not MonoBehaviour) return;
		//
		// 	GUI.Label(rect, content);
		// }
#endif
	}
}
