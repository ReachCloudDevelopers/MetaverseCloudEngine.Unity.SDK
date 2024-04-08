using MetaverseCloudEngine.Unity.Labels;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class OpenURL : MonoBehaviour
    {
        [SerializeField] private Label link;
        [SerializeField] private UnityEvent onOpened = new();
        [SerializeField] private UnityEvent onFailed = new();

        // - Deprecated -
        [SerializeField, HideInInspector] private string url;
        // -----

        public string Url
        {
            get => (string)link;
            set => link.SetValue(value);
        }

        private void Awake()
        {
            Upgrade();
        }

        private void OnValidate()
        {
            Upgrade();
        }

        private void Upgrade()
        {
            if (!string.IsNullOrEmpty(url))
            {
                link = url;
                url = null;
            }
        }

        public void Open()
        {
            Upgrade();
            link.GetValueAsync(v => MVUtils.OpenURL(v, () => onOpened?.Invoke(), () => onFailed?.Invoke()));
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(OpenURL))]
    public class OpenURLEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            UnityEditor.EditorGUILayout.HelpBox("For website links, you must use \"https://\" at the beginning. Also, \"http://\" is not supported.", UnityEditor.MessageType.Info);

            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();
        }
    }

#endif
}