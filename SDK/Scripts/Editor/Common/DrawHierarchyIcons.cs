using System;
using System.Linq;
using MetaverseCloudEngine.Unity.Attributes;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class DrawHierarchyIcons
    {
        private static int _hierarchyIconIndex;

        [InitializeOnLoadMethod]
        private static void RegisterHierarchyIconCallback()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyIcon_OnHierarchyWindowItemOnGUI;
        }
        
        private static void HierarchyIcon_OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            try
            {
                var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                if (!gameObject)
                    return;

                var components = gameObject.GetComponents<MonoBehaviour>()
                    .Distinct()
                    .Where(x => x)
                    .OrderBy(x => x.GetType().Name);

                Action lateAction = null;
            
                foreach (var c in components)
                {
                    var cType = c.GetType();
                    var iconAttributes = cType.GetCustomAttributes(typeof(HierarchyIconAttribute), true).FirstOrDefault();
                    if (iconAttributes == null)
                        continue;

                    try
                    {
                        var iconAttribute = (HierarchyIconAttribute)iconAttributes;
                        var icon = iconAttribute.IconName;
                        var iconTexture = EditorGUIUtility.IconContent(icon).image;
                        const int iconSize = 16;
                        var iconOffset = (iconSize * _hierarchyIconIndex) + (iconSize * 2);
                        var iconRect = new Rect(selectionRect.xMax - iconOffset, selectionRect.yMin, iconSize, iconSize);
                        if (iconRect.Contains(Event.current.mousePosition))
                        {
                            lateAction = () =>
                            {
                                var nicifyVariableName = ObjectNames.NicifyVariableName(cType.Name);
                                var labelWidth = EditorStyles.label.CalcSize(new GUIContent(nicifyVariableName)).x + 4;
                                var labelXOffset = iconSize + labelWidth;
                                EditorGUI.DrawRect(new Rect(iconRect.xMax - labelXOffset, iconRect.yMin, labelWidth, iconSize), new Color(0.1f, 0.1f, 0.1f, 0.9f));
                                EditorGUI.LabelField(new Rect(iconRect.xMax - labelXOffset, iconRect.yMin, labelWidth, iconSize), nicifyVariableName);
                            };
                        }
                        EditorGUI.DrawTextureAlpha(iconRect, iconTexture);
                    }
                    finally
                    {
                        _hierarchyIconIndex++;
                    }
                }
                
                lateAction?.Invoke();
            }
            finally
            {
                _hierarchyIconIndex = 0;
            }
        }
    }
}