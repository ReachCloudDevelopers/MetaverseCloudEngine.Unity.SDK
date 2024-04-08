using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [Serializable]
    public class MetaverseInteractableAttachPoint
    {
        [Flags]
        public enum AllowedNodes
        {
            LeftHand = 1,
            RightHand = 2,
        }

        [Flags]
        public enum AttachIndex
        {
            First = 1,
            Second = 2,
        }

        public Transform transform;
        public AllowedNodes allowedNodes = (AllowedNodes)~0;
        public AttachIndex attachIndex = (AttachIndex)~0;
        public MetaverseInteractableAttachPointEvents events = new();

        [NonSerialized] public bool IsInteracting;
    }

    [Serializable]
    public class MetaverseInteractableAttachPointEvents
    {
        public UnityEvent onSelectEntered;
        public UnityEvent onSelectExited;
    }
}