#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Installer
{
    internal sealed class MetaverseSdkUpdateInfo
    {
        internal string CurrentVersion { get; set; }
        internal string AvailableVersion { get; set; }
        internal string CommitHash { get; set; }
        internal string LatestEntry { get; set; }
        internal string FullChangelog { get; set; }
    }

    internal sealed class MetaverseSdkUpdateWindow : EditorWindow
    {
        private MetaverseSdkUpdateInfo _info;
        private Vector2 _scrollPosition;
        private bool _showFullChangelog;
        private bool _decisionMade;
        private bool _accepted;

        internal static bool ShowModal(MetaverseSdkUpdateInfo info)
        {
            var window = CreateInstance<MetaverseSdkUpdateWindow>();
            window._info = info;
            window.titleContent = new GUIContent("Metaverse Cloud Engine SDK Update");
            window.minSize = new Vector2(480f, 520f);
            window.maxSize = new Vector2(720f, 820f);
            window.ShowModalUtility();
            return window._accepted;
        }

        private void OnGUI()
        {
            if (_info == null)
            {
                Close();
                return;
            }

            EditorGUILayout.LabelField("A newer version of the Metaverse Cloud Engine SDK is available.", Styles.WordWrappedLabel);
            EditorGUILayout.Space();

            DrawVersionRow("Current version", _info.CurrentVersion);
            DrawVersionRow("Available version", _info.AvailableVersion);
            DrawVersionRow("Commit", string.IsNullOrEmpty(_info.CommitHash) ? "n/a" : _info.CommitHash);

            EditorGUILayout.Space();
            DrawChangelog();

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Skip for now", GUILayout.Width(140f)))
                {
                    _accepted = false;
                    _decisionMade = true;
                    Close();
                }

                if (GUILayout.Button("Install update", GUILayout.Width(140f)))
                {
                    _accepted = true;
                    _decisionMade = true;
                    Close();
                }
            }
        }

        private void DrawVersionRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, Styles.Label, GUILayout.Width(140f));
                GUILayout.Label(string.IsNullOrEmpty(value) ? "Unknown" : value, Styles.Value);
            }
        }

        private void DrawChangelog()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Changelog", Styles.SectionHeader);

                if (string.IsNullOrEmpty(_info.LatestEntry) && string.IsNullOrEmpty(_info.FullChangelog))
                {
                    GUILayout.Label("No changelog information is available for this update.", Styles.WordWrappedLabel);
                    return;
                }

                if (!string.IsNullOrEmpty(_info.FullChangelog))
                {
                    _showFullChangelog = EditorGUILayout.ToggleLeft("Show full changelog", _showFullChangelog);
                }

                var text = _showFullChangelog || string.IsNullOrEmpty(_info.LatestEntry)
                    ? _info.FullChangelog
                    : _info.LatestEntry;

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(260f));
                GUILayout.Label(text, Styles.Changelog);
                EditorGUILayout.EndScrollView();
            }
        }

        private void OnDisable()
        {
            if (!_decisionMade)
                _accepted = false;
        }

        private static class Styles
        {
            internal static readonly GUIStyle Label;
            internal static readonly GUIStyle Value;
            internal static readonly GUIStyle SectionHeader;
            internal static readonly GUIStyle WordWrappedLabel;
            internal static readonly GUIStyle Changelog;

            static Styles()
            {
                Label = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
                Value = new GUIStyle(EditorStyles.label) { wordWrap = true };
                SectionHeader = new GUIStyle(EditorStyles.boldLabel);
                WordWrappedLabel = new GUIStyle(EditorStyles.wordWrappedLabel);
                Changelog = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    richText = false
                };
            }
        }
    }
}
#endif
