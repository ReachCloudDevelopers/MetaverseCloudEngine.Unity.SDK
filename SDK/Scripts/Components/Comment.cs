#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Use this component to leave comments to other developers (and yourself) on GameObjects, prefabs, etc.
    /// Double-click the text area to edit.
    /// </summary>
    [AddComponentMenu(MetaverseConstants.ProductName + "/Utility/Comment")]
    public sealed class Comment : MonoBehaviour
    {
        /// <summary>
        /// The icon type to use for the comment.
        /// </summary>
        public enum IconType : byte
        {
            NoIcon,
            Info,
            Warning,
            WarningSoft,
            Error,
            ErrorSoft,
            Question,
            BugOk,
            BugWarning,
            LockRed,
            LockBlue,
            CrossRed,
            CrossBlue,
            CheckRed,
            CheckBlue,
            Star,
            Leaf,
        }

        [Tooltip("The text of the comment (supports rich text).")]
        [SerializeField] private string commentText;
        [Tooltip("The icon type of the comment.")]
        [SerializeField] private byte commentIconType;

        /// <summary>
        /// The text of the comment.
        /// </summary>
        public string CommentText { get => commentText; set => commentText = value; }
        /// <summary>
        /// The comment's icon type.
        /// </summary>
        public IconType CommentIconType { get => (IconType)commentIconType; set => commentIconType = (byte)value; }

#if UNITY_EDITOR
        [CustomEditor(typeof(Comment))]
        [CanEditMultipleObjects]
        sealed class CommentEditor : Editor
        {
            static readonly GUILayoutOption[] closeButtonOptions = { GUILayout.MinWidth(128f), GUILayout.Height(18f) };
            static readonly GUILayoutOption[] iconTypeDropdownOptions = { GUILayout.MinWidth(128f) };
            static GUIContent commentGuiContent;
            static GUIStyle textEditingStyle;
            static GUIStyle textPreviewStyle;
            static Texture2D[] icons;

            SerializedProperty propertyText;
            SerializedProperty propertyIconType;
            bool editable;

            void OnEnable()
            {
                propertyText = serializedObject.FindProperty(nameof(commentText));
                propertyIconType = serializedObject.FindProperty(nameof(commentIconType));

                // set as editable by default if the comment is empty
                if (string.IsNullOrEmpty(propertyText.stringValue))
                    editable = true;
            }

            static void SetupEditorStatics()
            {
                static Texture2D GetIcon(string id)
                    => EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_" + id : id).image as Texture2D;

                commentGuiContent ??= new GUIContent { tooltip = "Double-click to edit the comment" };
                textEditingStyle ??= new GUIStyle(EditorStyles.textField) { wordWrap = true };
                textPreviewStyle ??= new GUIStyle(EditorStyles.helpBox)
                {
                    wordWrap = true,
                    padding = new RectOffset(12, 12, 8, 8),
                    // change default font style and size here
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    // note that you can also use rich text in comments!
                    richText = true,
                };
                icons ??= new[]
                {
                GetIcon("console.infoicon.sml@2x"),
                GetIcon("console.warnicon.sml@2x"),
                GetIcon("console.warnicon.inactive.sml@2x"),
                GetIcon("console.erroricon.sml@2x"),
                GetIcon("console.erroricon.inactive.sml@2x"),
                GetIcon("P4_Conflicted@2x"),
                GetIcon("DebuggerAttached@2x"),
                GetIcon("DebuggerEnabled@2x"),
                GetIcon("P4_LockedLocal@2x"),
                GetIcon("P4_LockedRemote@2x"),
                GetIcon("P4_DeletedLocal@2x"),
                GetIcon("P4_DeletedRemote@2x"),
                GetIcon("P4_CheckOutLocal@2x"),
                GetIcon("P4_CheckOutRemote@2x"),
                GetIcon("Favorite Icon"),
                GetIcon("tree_icon_leaf"),
            };
            }

            public override void OnInspectorGUI()
            {
                SetupEditorStatics();

                if (editable)
                {
                    serializedObject.Update();

                    DrawEditableCommentGUI();

                    if (target)
                        serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    DrawCommentGUI();
                }
            }

            void DrawEditableCommentGUI()
            {
                string text = propertyText.stringValue;
                propertyText.stringValue = text = EditorGUILayout.TextArea(text, textEditingStyle);

                EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());

                // draw message type selector
                propertyIconType.intValue = (int)(IconType)EditorGUILayout.EnumPopup(
                    label: GUIContent.none,
                    selected: (IconType)propertyIconType.intValue,
                    iconTypeDropdownOptions
                );

                // draw the save/delete button
                if (string.IsNullOrEmpty(text))
                {
                    if (GUILayout.Button("Delete", closeButtonOptions))
                        DestroyImmediate(target);
                }
                else
                {
                    if (GUILayout.Button("Save", closeButtonOptions))
                        editable = false;
                }

                EditorGUILayout.EndHorizontal();
            }

            void DrawCommentGUI()
            {
                Comment target = this.target as Comment;
                Event currentEvent = Event.current;

                // draw non-editable comment text
                {
                    commentGuiContent.text = target.commentText;
                    commentGuiContent.image = target.commentIconType >= 1 && target.commentIconType < icons.Length + 1
                        ? icons[target.commentIconType - 1]
                        : null;

                    EditorGUILayout.LabelField(
                        label: GUIContent.none,
                        label2: commentGuiContent,
                        style: textPreviewStyle,
                        options: Array.Empty<GUILayoutOption>()
                    );
                }

                // begin editing comment on double-click
                {
                    // require mouse in comment area
                    if (!GUILayoutUtility.GetLastRect().Contains(currentEvent.mousePosition))
                        return;

                    // require double-click
                    if (currentEvent.clickCount < 2)
                        return;

                    // set as editable and refresh inspector
                    editable = true;
                    Repaint();
                }
            }
        }
#endif
    }
}