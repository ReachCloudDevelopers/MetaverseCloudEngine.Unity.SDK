using TriInspectorMVCE.Processors;
using TriInspectorMVCE;
using UnityEngine;

[assembly: RegisterTriPropertyHideProcessor(typeof(HideInPlayModeProcessor))]

namespace TriInspectorMVCE.Processors
{
    public class HideInPlayModeProcessor : TriPropertyHideProcessor<HideInPlayModeAttribute>
    {
        public override bool IsHidden(TriProperty property)
        {
            return Application.isPlaying != Attribute.Inverse;
        }
    }
}