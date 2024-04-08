using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [Experimental]
    [HideMonoScript]
    public abstract class AnimationMessageReceiverBase : TriInspectorMonoBehaviour
    {
        [SerializeField] private List<AnimationMessage> messages;

        private Dictionary<AnimationMessageHandle, List<AnimationMessage>> _messageLookup;

        public void AnimationEvent(AnimationEvent ev)
        {
            AnimationEventInternal(ev);
        }

        protected virtual bool AnimationEventInternal(AnimationEvent ev)
        {
            if (!isActiveAndEnabled)
                return false;
            
            if (messages == null || messages.Count == 0)
                return false;

            _messageLookup ??= messages.GroupBy(x => x.handle).ToDictionary(x => x.Key, y => y.ToList());

            if (ev.objectReferenceParameter is not AnimationMessageHandle handle)
                return false;

            if (!_messageLookup.TryGetValue(handle, out var messageList)) 
                return false;
            
            foreach (var receiver in messageList)
                receiver.OnReceived(handle);

            return true;
        }
    }
}