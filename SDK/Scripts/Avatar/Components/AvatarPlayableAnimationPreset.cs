using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [Serializable]
    [CreateAssetMenu(menuName = MetaverseConstants.MenuItems.MenuRootPath + "Avatar/Avatar Playable Animation")]
    public class AvatarPlayableAnimationPreset : ScriptableObject
    {
        public AnimationClip clip;
        public AvatarMask mask;
        public float fadeInTime;
        public float fadeOutTime;
    }
}