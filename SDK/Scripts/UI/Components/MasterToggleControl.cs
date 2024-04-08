using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class MasterToggleControl : MonoBehaviour
    {
        public void SetIsOn(bool value)
        {
            foreach (Toggle toggle in GetComponentsInChildren<Toggle>(true))
            {
                toggle.isOn = value;
            }
        }
        
        public void SetIsOnWithoutNotify(bool value)
        {
            foreach (Toggle toggle in GetComponentsInChildren<Toggle>(true))
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }
    }
    
#if UNITY_EDITOR
    // Just add an editor script that draws a not saying "Toggles all child toggles"
    [UnityEditor.CustomEditor(typeof(MasterToggleControl))]
    public class MasterToggleControlEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            UnityEditor.EditorGUILayout.HelpBox("This component allows you to control all child toggles.", UnityEditor.MessageType.Info);
        }
    }
#endif
}