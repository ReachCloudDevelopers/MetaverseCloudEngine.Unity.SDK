using UnityEditor;
using UnityEngine;
using MetaverseCloudEngine.Unity.Scripting.Components;
using static MetaverseCloudEngine.Unity.Scripting.Components.UnityEventType;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomPropertyDrawer(typeof(SerializableUnityEvent))]
    internal class SerializableUnityEventDrawer : PropertyDrawer
    {
        private const float Spacing = 4f;
        private const float HeaderHeight = 22f;
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var eventTypeProp = property.FindPropertyRelative("eventType");
            var eventType = (UnityEventType)eventTypeProp.enumValueIndex;
            
            SerializedProperty unityEventProp = GetUnityEventProperty(property, eventType);
            
            if (unityEventProp == null)
                return EditorGUIUtility.singleLineHeight;
            
            var height = HeaderHeight + Spacing; // Header (includes event name)
            height += EditorGUIUtility.singleLineHeight + Spacing; // Event type dropdown
            height += EditorGUI.GetPropertyHeight(unityEventProp, true) + Spacing; // Unity event
            
            return height;
        }

        private SerializedProperty GetUnityEventProperty(SerializedProperty property, UnityEventType eventType)
        {
            return eventType switch
            {
                UnityEventType.Void => property.FindPropertyRelative("unityEvent"),
                UnityEventType.Int => property.FindPropertyRelative("unityEventInt"),
                UnityEventType.Float => property.FindPropertyRelative("unityEventFloat"),
                UnityEventType.String => property.FindPropertyRelative("unityEventString"),
                UnityEventType.Bool => property.FindPropertyRelative("unityEventBool"),
                UnityEventType.Object => property.FindPropertyRelative("unityEventObject"),
                UnityEventType.Vector2 => property.FindPropertyRelative("unityEventVector2"),
                UnityEventType.Vector3 => property.FindPropertyRelative("unityEventVector3"),
                UnityEventType.Vector4 => property.FindPropertyRelative("unityEventVector4"),
                UnityEventType.Quaternion => property.FindPropertyRelative("unityEventQuaternion"),
                UnityEventType.GameObject => property.FindPropertyRelative("unityEventGameObject"),
                UnityEventType.Transform => property.FindPropertyRelative("unityEventTransform"),
                UnityEventType.Collider => property.FindPropertyRelative("unityEventCollider"),
                UnityEventType.Texture => property.FindPropertyRelative("unityEventTexture"),
                UnityEventType.Texture2D => property.FindPropertyRelative("unityEventTexture2D"),
                UnityEventType.Sprite => property.FindPropertyRelative("unityEventSprite"),
                _ => property.FindPropertyRelative("unityEvent")
            };
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var eventNameProp = property.FindPropertyRelative("eventName");
            var eventTypeProp = property.FindPropertyRelative("eventType");
            
            if (eventNameProp == null || eventTypeProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Error: Missing properties");
                return;
            }
            
            var eventType = (UnityEventType)eventTypeProp.enumValueIndex;
            var unityEventProp = GetUnityEventProperty(property, eventType);
            
            if (unityEventProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Error: Missing event property");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            
            var currentY = position.y;
            
            // Draw outer box encapsulating everything
            var outerBoxRect = new Rect(position.x - 2, position.y, position.width + 4, position.height);
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUI.Box(outerBoxRect, GUIContent.none, EditorStyles.helpBox);
            GUI.backgroundColor = oldColor;
            
            // Draw header background
            var headerRect = new Rect(position.x, currentY, position.width, HeaderHeight);
            var headerBgRect = new Rect(headerRect.x, headerRect.y, headerRect.width, headerRect.height);
            
            // Header background
            GUI.backgroundColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            GUI.Box(headerBgRect, GUIContent.none, EditorStyles.toolbar);
            GUI.backgroundColor = oldColor;
            
            // Draw icon
            var icon = Resources.Load<Texture2D>("PlatformIcons/event");
            var iconRect = new Rect(headerRect.x + 8, headerRect.y + 3, 16, 16);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon);
            }
            
            // Draw event name as borderless text field in header
            var eventNameFieldRect = new Rect(headerRect.x + 28, headerRect.y + 2, headerRect.width - 36, headerRect.height - 4);
            
            // Create a borderless text field style
            var borderlessTextFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(4, 4, 2, 2),
                normal = { background = null, textColor = EditorStyles.boldLabel.normal.textColor },
                focused = { background = EditorGUIUtility.whiteTexture, textColor = EditorStyles.textField.focused.textColor },
                hover = { background = null, textColor = EditorStyles.boldLabel.normal.textColor }
            };
            
            // Show placeholder text if empty
            var displayText = string.IsNullOrEmpty(eventNameProp.stringValue) ? "(Unnamed Event)" : eventNameProp.stringValue;
            var previousColor = GUI.color;
            
            if (string.IsNullOrEmpty(eventNameProp.stringValue))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f); // Dimmed for placeholder
            }
            
            EditorGUI.BeginChangeCheck();
            var newEventName = EditorGUI.TextField(eventNameFieldRect, eventNameProp.stringValue, borderlessTextFieldStyle);
            if (EditorGUI.EndChangeCheck())
            {
                eventNameProp.stringValue = newEventName;
            }
            
            GUI.color = previousColor;
            
            currentY += HeaderHeight + Spacing;
            
            // Draw event type dropdown
            var eventTypeRect = new Rect(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(eventTypeRect, eventTypeProp, new GUIContent("Type", "The parameter type for this event"));
            
            currentY += EditorGUIUtility.singleLineHeight + Spacing;
            
            // Draw UnityEvent field
            var unityEventRect = new Rect(position.x, currentY, position.width, EditorGUI.GetPropertyHeight(unityEventProp, true));
            
            // Custom label for UnityEvent based on type
            string eventLabel = eventType switch
            {
                UnityEventType.Void => "Listeners",
                UnityEventType.Int => "Listeners (int)",
                UnityEventType.Float => "Listeners (float)",
                UnityEventType.String => "Listeners (string)",
                UnityEventType.Bool => "Listeners (bool)",
                UnityEventType.Object => "Listeners (Object)",
                UnityEventType.Vector2 => "Listeners (Vector2)",
                UnityEventType.Vector3 => "Listeners (Vector3)",
                UnityEventType.Vector4 => "Listeners (Vector4)",
                UnityEventType.Quaternion => "Listeners (Quaternion)",
                UnityEventType.GameObject => "Listeners (GameObject)",
                UnityEventType.Transform => "Listeners (Transform)",
                UnityEventType.Collider => "Listeners (Collider)",
                UnityEventType.Texture => "Listeners (Texture)",
                UnityEventType.Texture2D => "Listeners (Texture2D)",
                UnityEventType.Sprite => "Listeners (Sprite)",
                _ => "Listeners"
            };
            
            var unityEventLabel = new GUIContent(eventLabel, "The UnityEvent listeners to invoke");
            EditorGUI.PropertyField(unityEventRect, unityEventProp, unityEventLabel, true);
            
            EditorGUI.EndProperty();
        }
    }
}

