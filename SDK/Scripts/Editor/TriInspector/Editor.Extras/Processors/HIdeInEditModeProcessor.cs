using TriInspectorMVCE.Processors;
using TriInspectorMVCE;
using UnityEngine;

[assembly: RegisterTriPropertyHideProcessor(typeof(HideInEditModeProcessor))]

namespace TriInspectorMVCE.Processors
{
    public class HideInEditModeProcessor : TriPropertyHideProcessor<HideInEditModeAttribute>
    {
        public override bool IsHidden(TriProperty property)
        {
            return Application.isPlaying == Attribute.Inverse;
        }
    }
}