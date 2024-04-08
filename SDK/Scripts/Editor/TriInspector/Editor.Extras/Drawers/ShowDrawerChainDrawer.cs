using System.Collections.Generic;
using System.Text;
using TriInspectorMVCE;
using TriInspectorMVCE.Drawers;
using TriInspectorMVCE.Elements;

[assembly: RegisterTriAttributeDrawer(typeof(ShowDrawerChainDrawer), TriDrawerOrder.System)]

namespace TriInspectorMVCE.Drawers
{
    public class ShowDrawerChainDrawer : TriAttributeDrawer<ShowDrawerChainAttribute>
    {
        public override TriElement CreateElement(TriProperty property, TriElement next)
        {
            return new TriDrawerChainInfoElement(property.AllDrawers, next);
        }
    }

    public class TriDrawerChainInfoElement : TriElement
    {
        public TriDrawerChainInfoElement(IReadOnlyList<TriCustomDrawer> drawers, TriElement next)
        {
            var info = new StringBuilder();

            info.Append("Drawer Chain:");

            for (var i = 0; i < drawers.Count; i++)
            {
                var drawer = drawers[i];
                info.AppendLine();
                info.Append(i).Append(": ").Append(drawer.GetType().Name);
            }

            AddChild(new TriInfoBoxElement(info.ToString()));
            AddChild(next);
        }
    }
}