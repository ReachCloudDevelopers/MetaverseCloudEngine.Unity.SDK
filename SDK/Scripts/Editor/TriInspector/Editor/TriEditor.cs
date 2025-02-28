using System;
using TriInspectorMVCE.Utilities;
using UnityEditor;
using UnityEngine;

namespace TriInspectorMVCE
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TriInspectorMonoBehaviour), editorForChildClasses: true, isFallback = true)]
    internal class TriMonoBehaviourEditor : TriEditor
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), editorForChildClasses: true, isFallback = true)]
    internal class TriScriptableObjectEditor : TriEditor
    {
    }

    public class TriEditor : Editor
    {
        private TriPropertyTreeForSerializedObject _inspector;

        private void OnDisable()
        {
            _inspector?.Dispose();
            _inspector = null;
        }
        
        public override void OnInspectorGUI()
        {
            OnInspectorGUI(null);
        }

        public void OnInspectorGUI(float? viewWidth)
        {
            if (serializedObject.targetObjects.Length == 0)
            {
                return;
            }

            if (serializedObject.targetObject == null)
            {
                EditorGUILayout.HelpBox("Script is missing", MessageType.Warning);
                return;
            }

            if (TriGuiHelper.IsEditorTargetPushed(serializedObject.targetObject))
            {
                GUILayout.Label("Recursive inline editors not supported");
                return;
            }

            if (_inspector == null)
            {
                _inspector = new TriPropertyTreeForSerializedObject(serializedObject);
            }

            serializedObject.UpdateIfRequiredOrScript();

            _inspector.Update();

            if (_inspector.ValidationRequired)
            {
                _inspector.RunValidation();
            }

            using (TriGuiHelper.PushEditorTarget(target))
            {
                _inspector.Draw(viewWidth);
            }

            try
            {
                if (serializedObject.ApplyModifiedProperties())
                {
                    _inspector.RequestValidation();
                }

                if (_inspector.RepaintRequired)
                {
                    Repaint();
                }
            }
            catch
            {
                GUIUtility.ExitGUI();
            }
        }
    }
}