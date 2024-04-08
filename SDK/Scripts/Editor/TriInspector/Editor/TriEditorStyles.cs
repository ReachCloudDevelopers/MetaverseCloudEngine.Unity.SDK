using UnityEditor;
using UnityEngine;

namespace TriInspectorMVCE
{
    public static class TriEditorStyles
    {
        private const string BaseResourcesPath = "Packages/com.codewriter.triinspector/Editor/Resources/";

        private static GUIStyle _contentBox;
        private static GUIStyle _box;

        public static GUIStyle TabOnlyOne { get; } = "Tab onlyOne";
        public static GUIStyle TabFirst { get; } = "Tab first";
        public static GUIStyle TabMiddle { get; } = "Tab middle";
        public static GUIStyle TabLast { get; } = "Tab last";

        public static GUIStyle ContentBox
        {
            get
            {
                if (_contentBox == null)
                {
                    _contentBox = new GUIStyle
                    {
                        border = new RectOffset(2, 2, 2, 2),
                        normal =
                        {
                            background = LoadTexture("TriInspector_Content_Bg"),
                        },
                    };
                }

                return _contentBox;
            }
        }

        public static GUIStyle Box
        {
            get
            {
                if (_box == null)
                {
                    _box = new GUIStyle
                    {
                        border = new RectOffset(2, 2, 2, 2),
                        normal =
                        {
                            background = LoadTexture("TriInspector_Box_Bg"),
                        },
                    };
                }

                return _box;
            }
        }

        private static Texture2D LoadTexture(string name)
        {
            return EditorGUIUtility.isProSkin
                ? Resources.Load<Texture2D>(name + "_Dark")
                : Resources.Load<Texture2D>(name);
        }
    }
}