using TriInspectorMVCE;
using TriInspectorMVCE.Drawers;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(GUIColorDrawer), TriDrawerOrder.Decorator)]

namespace TriInspectorMVCE.Drawers
{
    public class GUIColorDrawer : TriAttributeDrawer<GUIColorAttribute>
    {
        public override void OnGUI(Rect position, TriProperty property, TriElement next)
        {
            var oldColor = GUI.color;
            var newColor = new Color(Attribute.R, Attribute.G, Attribute.B, Attribute.A);

            GUI.color = newColor;
            GUI.contentColor = newColor;

            next.OnGUI(position);

            GUI.color = oldColor;
            GUI.contentColor = oldColor;
        }
    }
}