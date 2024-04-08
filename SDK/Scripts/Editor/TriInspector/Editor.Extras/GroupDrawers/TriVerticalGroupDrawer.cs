using TriInspectorMVCE;
using TriInspectorMVCE.Elements;
using TriInspectorMVCE.GroupDrawers;

[assembly: RegisterTriGroupDrawer(typeof(TriVerticalGroupDrawer))]

namespace TriInspectorMVCE.GroupDrawers
{
    public class TriVerticalGroupDrawer : TriGroupDrawer<DeclareVerticalGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareVerticalGroupAttribute attribute)
        {
            return new TriVerticalGroupElement();
        }
    }
}