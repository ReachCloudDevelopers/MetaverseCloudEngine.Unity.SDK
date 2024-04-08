using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-int.MaxValue)]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Platform/Offline Only")]
    public partial class OfflineOnly : MonoBehaviour
    {
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(OfflineOnly))]
    public class OfflineOnlyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            UnityEditor.EditorGUILayout.HelpBox(
                "This object (including all of it's children) will be destroyed " +
                "if this is the actual runtime version of the app. NOTE: All " +
                "objects will still be included in meta space/prefab uploads."
                , UnityEditor.MessageType.Info);
            
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}