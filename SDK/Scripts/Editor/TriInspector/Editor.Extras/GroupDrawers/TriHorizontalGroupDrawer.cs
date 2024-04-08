using TriInspectorMVCE;
using TriInspectorMVCE.Elements;
using TriInspectorMVCE.GroupDrawers;
using UnityEngine;

[assembly: RegisterTriGroupDrawer(typeof(TriHorizontalGroupDrawer))]

namespace TriInspectorMVCE.GroupDrawers
{
    public class TriHorizontalGroupDrawer : TriGroupDrawer<DeclareHorizontalGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareHorizontalGroupAttribute attribute)
        {
            return new TriHorizontalGroupElement();
        }
    }
}