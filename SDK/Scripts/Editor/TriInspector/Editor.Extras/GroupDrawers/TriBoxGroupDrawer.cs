using TriInspectorMVCE;
using TriInspectorMVCE.Elements;
using TriInspectorMVCE.GroupDrawers;

[assembly: RegisterTriGroupDrawer(typeof(TriBoxGroupDrawer))]

namespace TriInspectorMVCE.GroupDrawers
{
    public class TriBoxGroupDrawer : TriGroupDrawer<DeclareBoxGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareBoxGroupAttribute attribute)
        {
            return new TriBoxGroupElement(new TriBoxGroupElement.Props
            {
                title = attribute.Title,
                titleMode = attribute.HideTitle
                    ? TriBoxGroupElement.TitleMode.Hidden
                    : TriBoxGroupElement.TitleMode.Normal,
            });
        }
    }
}