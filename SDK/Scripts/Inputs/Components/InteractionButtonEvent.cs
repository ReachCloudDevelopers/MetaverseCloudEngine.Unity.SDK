using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;

#if MV_XR_TOOLKIT_3
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
#else
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable;
#endif

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