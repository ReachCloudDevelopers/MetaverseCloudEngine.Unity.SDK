using TriInspectorMVCE.Processors;
using TriInspectorMVCE;
using UnityEngine;

[assembly: RegisterTriPropertyDisableProcessor(typeof(DisableInEditModeProcessor))]

namespace TriInspectorMVCE.Processors
{
    public class DisableInEditModeProcessor : TriPropertyDisableProcessor<DisableInEditModeAttribute>
    {
        public override bool IsDisabled(TriProperty property)
        {
            return Application.isPlaying == Attribute.Inverse;
        }
    }
}