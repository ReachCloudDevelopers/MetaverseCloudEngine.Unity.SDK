using TriInspectorMVCE;
using TriInspectorMVCE.Elements;
using TriInspectorMVCE.GroupDrawers;

[assembly: RegisterTriGroupDrawer(typeof(TriTabGroupDrawer))]

namespace TriInspectorMVCE.GroupDrawers
{
    public class TriTabGroupDrawer : TriGroupDrawer<DeclareTabGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareTabGroupAttribute attribute)
        {
            return new TriTabGroupElement();
        }
    }
}