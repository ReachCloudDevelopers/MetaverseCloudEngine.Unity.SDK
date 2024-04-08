using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Animations;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    /// <summary>
    /// A helper component that provides the ability to play custom animations on a 
    /// player avatar at runtime.
    /// </summary>
    [Experimental]
    [HideMonoScript]
    public class PlayAvatarAnimation : TriInspectorMonoBehaviour
    {
        [Tooltip("(Optional) The avatar container object. If left blank, this will be automatically found.")]
        public PlayerAvatarContainer container;
        [Tooltip("(Optional) The animation preset to use.")]
        public AvatarPlayableAnimationPreset preset;

        private float _lastFadeOutTime;
        private AnimationClipPlayable _lastPlayedAnimation;

        private void Start() { /* for enabled/disabled checkbox */ }

        /// <summary>
        /// Stops all the currently active animations on the avatar.
        /// </summary>
        public void StopAllAvatarAnimations()
        {
            if (!isActiveAndEnabled)
                return;

            if (!FindContainer())
                return;

            container.StopAllAnimations();
        }

        /// <summary>
        /// Stops the last played animation if it is currently playing.
        /// </summary>
        public void StopLastAnimation()
        {
            if (!isActiveAndEnabled)
                return;

            if (!FindContainer())
                return;

            container.StopAnimation(_lastPlayedAnimation, _lastFadeOutTime);
        }

        /// <summary>
        /// Plays the <see cref="preset"/> on the <see cref="container"/> avatar.
        /// </summary>
        public void Play()
        {
            Play(preset);
        }

        /// <summary>
        /// Plays the <paramref name="preset"/> on the <see cref="container"/> avatar.
        /// </summary>
        /// <param name="preset">The animation preset.</param>
        public void Play(AvatarPlayableAnimationPreset preset)
        {
            if (!isActiveAndEnabled)
                return;

            if (!preset || !preset.clip)
                return;

            if (!FindContainer())
                return;

            _lastPlayedAnimation = container.PlayAnimation(
                preset.clip,
                preset.clip.isLooping ? Mathf.Infinity : preset.clip.length,
                mask: preset.mask,
                fadeInTime: preset.fadeInTime,
                fadeOutTime: preset.fadeOutTime);
            _lastFadeOutTime = preset.fadeOutTime;
        }

        private bool FindContainer()
        {
            if (container) 
                return true;
            
            container = gameObject.GetComponentInParent<PlayerAvatarContainer>();
            if (!container) container = gameObject.GetComponentInChildren<PlayerAvatarContainer>();
            if (container) 
                return true;
            
            return enabled = false;

        }
    }
}