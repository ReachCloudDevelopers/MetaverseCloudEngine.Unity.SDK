using TriInspectorMVCE.Processors;
using TriInspectorMVCE;
using UnityEngine;

[assembly: RegisterTriPropertyDisableProcessor(typeof(DisableInPlayModeProcessor))]

namespace TriInspectorMVCE.Processors
{
    public class DisableInPlayModeProcessor : TriPropertyDisableProcessor<DisableInPlayModeAttribute>
    {
        public override bool IsDisabled(TriProperty property)
        {
            return Application.isPlaying != Attribute.Inverse;
        }
    }
}