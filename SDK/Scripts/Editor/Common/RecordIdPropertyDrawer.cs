using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class RecordIdPropertyDrawer<TRecord, TRecordId, TPickerEditor> : PropertyDrawer 
        where TPickerEditor : PickerEditor
        where TRecord : class
    {
        private static readonly Dictionary<TRecordId, TRecord> Records = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position = EditorGUI.PrefixLabel(position, label);
            
            var objectFieldPos = new Rect(position)
            {
                width = position.width - 25
            };

            GUI.Label(objectFieldPos, GUIContent.none, EditorStyles.objectField);

            var labelPos = new Rect(position)
            {
                width = position.width - 25
            };

            if (!string.IsNullOrEmpty(property.stringValue))
            {
                var recordId = ParseRecordId(property.stringValue);

                if (recordId != null && !recordId.Equals(default(TRecordId)) && Records.TryGetValue(recordId, out var record) && Records[recordId] != null)
                {
                    try
                    {
                        EditorGUIUtility.SetIconSize(Vector2.one * 12);
                        EditorGUI.LabelField(labelPos, GetRecordLabel(record), EditorStyles.miniBoldLabel);
                    }
                    finally
                    {
                        EditorGUIUtility.SetIconSize(Vector2.zero);
                    }
                }
                else
                {
                    if (recordId != null && !Records.ContainsKey(recordId))
                    {
                        Records[recordId] = null;
                        RequestRecord(recordId, r => Records[recordId] = r);
                    }
                    else
                    {
                        EditorGUI.LabelField(labelPos, property.stringValue + " (Loading...)", EditorStyles.miniBoldLabel);
                    }
                }
            }

            var buttonPos = new Rect(position)
            {
                x = labelPos.position.x + labelPos.width,
                width = 25
            };

            if (GUI.Button(buttonPos, EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Linked" : "Linked"), EditorStyles.miniButtonRight))
            {
                // Check if right click
                if (Event.current.button == 1)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear"), false, () =>
                    {
                        property.stringValue = null;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    if (!string.IsNullOrEmpty(property.stringValue))
                    {
                        menu.AddItem(new GUIContent("Copy"), false, () => EditorGUIUtility.systemCopyBuffer = property.stringValue);
                    }
                    if (EditorGUIUtility.systemCopyBuffer != null)
                    {
                        var recordId = ParseRecordId(EditorGUIUtility.systemCopyBuffer);
                        if (recordId is not null)
                        {
                            var recordIdString = GetRecordIdStringValue(recordId);
                            if (!string.IsNullOrEmpty(recordIdString))
                                menu.AddItem(new GUIContent("Paste"), false, () =>
                                {
                                    property.stringValue = GetRecordIdStringValue(recordId);
                                    property.serializedObject.ApplyModifiedProperties();
                                });
                        }
                    }
                    menu.ShowAsContext();
                    return;
                }
                
                PickerEditor.Pick<TPickerEditor>(o =>
                {
                    if (o == null)
                    {
                        property.stringValue = null;
                        property.serializedObject.ApplyModifiedProperties();
                        return;
                    }
                    
                    var record = (TRecord)o;
                    var recordId = GetRecordId(record);
                    Records[recordId] = record;
                    property.stringValue = GetRecordIdStringValue(recordId);
                    property.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        protected abstract TRecordId ParseRecordId(string str);
        protected abstract TRecordId GetRecordId(TRecord record);
        protected abstract string GetRecordIdStringValue(TRecordId id);
        protected abstract GUIContent GetRecordLabel(TRecord record);
        protected abstract void RequestRecord(TRecordId id, Action<TRecord> onSuccess, Action onFailed = null);
    }
}
