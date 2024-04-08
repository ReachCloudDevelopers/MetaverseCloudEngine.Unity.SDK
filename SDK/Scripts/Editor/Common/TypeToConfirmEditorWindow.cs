using System;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class TypeToConfirmEditorWindow : EditorWindow
    {
        private static TypeToConfirmEditorWindow _window;
        private static Action _onConfirm;
        private static Action _onCancel;
        private static string _message;
        private static string _confirmText;
        private static string _confirmButtonText;
        private static string _cancelButtonText;
        private string _currentConfirmText;

        public static void Open(string message, string confirmText, string confirmButtonText,
            string cancelButtonText, Action onConfirm, Action onCancel = null)
        {
            _message = message;
            _confirmText = confirmText;
            _confirmButtonText = confirmButtonText;
            _cancelButtonText = cancelButtonText;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _window = GetWindow<TypeToConfirmEditorWindow>();
            _window.titleContent = new GUIContent("Confirm", MetaverseEditorUtils.EditorIcon);
            _window.maxSize = _window.minSize = new Vector2(400, 300);
            _window.ShowModalUtility();
        }

        private void OnGUI()
        {
            MetaverseEditorUtils.Header("Confirm");
            EditorGUILayout.HelpBox(_message, MessageType.Error);
            EditorGUILayout.HelpBox("Type '" + _confirmText + "' to confirm.", MessageType.None);
            _currentConfirmText = EditorGUILayout.TextField(_currentConfirmText);
            if (_currentConfirmText != _confirmText)
            {
                EditorGUILayout.HelpBox("Please type the confirmation text to continue.", MessageType.Warning);
                GUI.enabled = false;
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(_confirmButtonText))
            {
                _onConfirm?.Invoke();
                _window.Close();
            }

            GUI.enabled = true;

            if (GUILayout.Button(_cancelButtonText))
            {
                _onCancel?.Invoke();
                _window.Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}