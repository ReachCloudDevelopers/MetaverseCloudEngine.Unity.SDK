using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine.XR.Interaction.Toolkit;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class InteractionButtonEvent : InputButtonEvent
    {
        [PropertyOrder(-999)]
        [DisallowNull] public XRBaseInteractable interactable;
        [PropertyOrder(-998)]
        public bool listenOnHover;

        protected override bool ShouldPerform()
        {
            return interactable && (interactable.isSelected || (listenOnHover && interactable.isHovered));
        }
    }
}