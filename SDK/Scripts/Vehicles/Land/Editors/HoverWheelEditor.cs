﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [CustomEditor(typeof(HoverWheel))]
    [CanEditMultipleObjects]

    public class HoverWheelEditor : Editor
    {
        bool isPrefab = false;
        static bool showButtons = true;

        public override void OnInspectorGUI() {
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;
            HoverWheel targetScript = (HoverWheel)target;
            HoverWheel[] allTargets = new HoverWheel[targets.Length];
            isPrefab = F.IsPrefab(targetScript);

            for (int i = 0; i < targets.Length; i++) {
                Undo.RecordObject(targets[i], "Hover Wheel Change");
                allTargets[i] = targets[i] as HoverWheel;
            }

            DrawDefaultInspector();

            if (!isPrefab && targetScript.gameObject.activeInHierarchy) {
                showButtons = EditorGUILayout.Foldout(showButtons, "Quick Actions", boldFoldout);
                EditorGUI.indentLevel++;
                if (showButtons) {
                    if (GUILayout.Button("Get Visual Wheel")) {
                        foreach (HoverWheel curTarget in allTargets) {
                            if (curTarget.transform.childCount > 0) {
                                curTarget.visualWheel = curTarget.transform.GetChild(0);
                            }
                            else {
                                Debug.LogWarning("No visual wheel found.", this);
                            }
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }

            if (GUI.changed) {
                EditorUtility.SetDirty(targetScript);
            }
        }
    }
}
#endif