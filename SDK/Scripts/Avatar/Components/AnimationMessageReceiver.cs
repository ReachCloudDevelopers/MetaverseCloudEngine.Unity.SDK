using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [HideMonoScript]
    [RequireComponent(typeof(Animator))]
    public class AnimationMessageReceiver : AnimationMessageReceiverBase
    {
        protected override bool AnimationEventInternal(AnimationEvent ev)
        {
            var children = GetComponentsInChildren<AnimationMessageReceiverChild>(true);
            foreach (var child in children)
                child.AnimationEvent(ev);
            return base.AnimationEventInternal(ev);
        }
    }
}