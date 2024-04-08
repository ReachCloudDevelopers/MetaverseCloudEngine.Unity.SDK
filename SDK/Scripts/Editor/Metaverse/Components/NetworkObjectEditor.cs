using MetaverseCloudEngine.Unity.Networking.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    [CustomEditor(typeof(NetworkObject))]
    public class NetworkObjectEditor : UnityEditor.Editor
    {
        private NetworkObject _netObj;

        private void OnEnable()
        {
            _netObj = target as NetworkObject;
        }

        public override void OnInspectorGUI()
        {
            if (_netObj == null)
            {
                GUIUtility.ExitGUI();
                return;
            }

            serializedObject.Update();

            try
            {
                if (!_netObj.gameObject.IsPrefab())
                {
                    if (Application.isPlaying)
                    {
                        GUI.enabled = false;

                        if (MetaverseProgram.Initialized && _netObj.Networking != null)
                        {
                            EditorGUILayout.BeginVertical("box");
                            EditorGUILayout.Toggle("Is Initialized", _netObj.IsInitialized);
                            EditorGUILayout.Toggle("Has Input Authority", _netObj.IsInputAuthority);
                            EditorGUILayout.IntField("Input Authority ID", _netObj.InputAuthorityID);
                            EditorGUILayout.Toggle("Has State Authority", _netObj.IsStateAuthority);
                            EditorGUILayout.IntField("State Authority ID", _netObj.StateAuthorityID);
                            EditorGUILayout.EndVertical();
                        }

                        GUI.enabled = true;
                    }
                }
                else if (_netObj.transform.parent)
                {
                    EditorGUILayout.HelpBox(
                        "Network Objects generally should not be on a child game object within the hierarchy of a prefab. " +
                        "You should move this to the root of the prefab hierarchy. However, if you're " +
                        "nesting meta prefabs, or the root prefab is not being instantiated, this is okay.",
                        MessageType.Warning);
                }

                DrawPropertiesExcluding(serializedObject, "m_Script");
            }
            finally
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}