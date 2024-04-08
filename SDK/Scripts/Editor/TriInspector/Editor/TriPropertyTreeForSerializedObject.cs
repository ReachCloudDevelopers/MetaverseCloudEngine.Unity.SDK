using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace TriInspectorMVCE
{
    public sealed class TriPropertyTreeForSerializedObject : TriPropertyTree
    {
        private readonly SerializedObject _serializedObject;
        private readonly SerializedProperty _scriptProperty;

        public TriPropertyTreeForSerializedObject([NotNull] SerializedObject serializedObject)
        {
            _serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            _scriptProperty = serializedObject.FindProperty("m_Script");

            TargetObjectType = _serializedObject.targetObject.GetType();
            TargetsCount = _serializedObject.targetObjects.Length;
            TargetIsPersistent = _serializedObject.targetObject is var targetObject &&
                                 targetObject != null && EditorUtility.IsPersistent(targetObject);

            RootPropertyDefinition = new TriPropertyDefinition(
                memberInfo: null,
                ownerType: null,
                order: -1,
                fieldName: "ROOT",
                fieldType: TargetObjectType,
                valueGetter: (_, targetIndex) => _serializedObject.targetObjects[targetIndex],
                valueSetter: (_, targetIndex, _) => _serializedObject.targetObjects[targetIndex],
                attributes: new List<Attribute>(),
                isArrayElement: false);

            RootProperty = new TriProperty(this, null, RootPropertyDefinition, serializedObject);

            RootProperty.ValueChanged += OnPropertyChanged;
            RootProperty.ChildValueChanged += OnPropertyChanged;
        }

        public override void Dispose()
        {
            RootProperty.ChildValueChanged -= OnPropertyChanged;
            RootProperty.ValueChanged -= OnPropertyChanged;

            base.Dispose();
        }

        public override void Update(bool forceUpdate = false)
        {
            if (forceUpdate)
            {
                if (_serializedObject == null)
                    return;

                try
                {
                    _serializedObject.SetIsDifferentCacheDirty();
                    _serializedObject.Update();
                }
                catch (ArgumentNullException)
                {
                    return;
                }
            }

            base.Update(forceUpdate);
        }

        public override void Draw(float? viewWidth = null)
        {
            DrawMonoScriptProperty();
            DrawOtherProperties();
            base.Draw(viewWidth);
        }

        public override bool ApplyChanges()
        {
            var changed = base.ApplyChanges();
            changed |= _serializedObject.ApplyModifiedProperties();
            return changed;
        }

        public override void ForceCreateUndoGroup()
        {
            Undo.RegisterCompleteObjectUndo(_serializedObject.targetObjects, "Inspector");
            Undo.FlushUndoRecordObjects();
        }

        private void OnPropertyChanged(TriProperty changedProperty)
        {
            foreach (var targetObject in _serializedObject.targetObjects)
                EditorUtility.SetDirty(targetObject);
            RequestValidation();
            RequestRepaint();
        }

        private void DrawMonoScriptProperty()
        {
            if (RootProperty.TryGetAttribute(out HideMonoScriptAttribute _))
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            var scriptRect = EditorGUILayout.GetControlRect(true);
            scriptRect.xMin += 3;
            EditorGUI.PropertyField(scriptRect, _scriptProperty);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawOtherProperties()
        {
            if (RootProperty.TryGetAttribute(out ExperimentalAttribute _))
            {
                // Display message: "This component is experimental and it's behavior may change, or it may be removed in future versions."
                var rect = EditorGUILayout.GetControlRect(true, GUILayout.Height(36));
                rect.xMin += 3;
                EditorGUI.HelpBox(rect, "This component is experimental and it's behavior may change, or it may be removed in future versions.", MessageType.Warning);
            }
        }
    }
}