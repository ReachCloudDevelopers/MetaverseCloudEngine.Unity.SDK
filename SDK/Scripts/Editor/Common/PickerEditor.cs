using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class PickerEditor : EditorWindow
    {
        private Action<object> _onSelectedAction;
        private PaginatedEditor<object> _paginatedEditor;

        public virtual string Title => "Select " + GetType().Name.Replace("Picker", string.Empty).Replace("Editor", string.Empty);
        protected virtual string Error { get; set; }
        public static GUIContent DefaultIcon { get; private set; }

        public static void Pick<TWindow>(Action<object> onSelectedAction) where TWindow : PickerEditor
        {
            var window = GetWindow<TWindow>(true);

            var pos = window.position;
            pos.center = EditorGUIUtility.GetMainWindowPosition().center;
            window.position = pos;

            var size = window.minSize;
            size.x = 300;
            size.y = 400;
            window.minSize = size;
            size = window.maxSize;
            size.x = 500;
            size.y = 400;
            window.maxSize = size;

            window.titleContent = new GUIContent(window.Title);
            window.Show();
            window._onSelectedAction = onSelectedAction;
        }

        private void OnEnable()
        {
            _paginatedEditor = new PaginatedEditor<object>(GUIContent.none)
            {
                DisplayAddButton = false
            };
            _paginatedEditor.DrawRecord += OnDrawRecord;
            _paginatedEditor.BeginRequest += OnBeginRequest;

            if (DefaultIcon == null)
                DefaultIcon = EditorGUIUtility.IconContent("RawImage Icon");
        }

        private void OnGUI()
        {
            _paginatedEditor.Draw();
        }

        private void Update()
        {
            Repaint();
        }

        private bool OnBeginRequest(int offset, int count, string filter)
        {
            return RequestPickables(offset, count, filter);
        }

        private bool OnDrawRecord(object record)
        {
            if (GUILayout.Button(record != null ? GetPickableContent(record) : new GUIContent("None"), EditorStyles.toolbarButton, GUILayout.MaxWidth(position.width)))
            {
                try { _onSelectedAction?.Invoke(record); }
                catch { /* ignored */ }
                Close();
            }

            EditorGUILayout.Space();
            return true;
        }

        protected virtual GUIContent GetPickableContent(object pickable) => new (pickable?.ToString());

        protected abstract bool RequestPickables(int offset, int count, string filter);

        protected void OnPickablesReceived(object[] pickables)
        {
            _paginatedEditor.EndRequest(new [] { (object)null }.Concat(pickables));
        }
    }
}
