using TriInspectorMVCE;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [HideMonoScript]
    public class FootstepSoundHandler : TriInspectorMonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource footstepAudioSource;
        [SerializeField] private AudioClip[] defaultFootstepSounds;
        [SerializeField] private LayerMask footstepLayerMask;
        [SerializeField, Min(0)] private float audioListenerCullDistance = 10f;
        [SerializeField, HideInInspector] private float audioListenerCullDistanceSqr;
        [SerializeField, Min(0)] private float minSoundInterval = 0.25f;
        [SerializeField, Range(0, 2)] private float minPitch = 0.9f;
        [SerializeField, Range(0, 2)] private float maxPitch = 1.1f;
        
        private static AudioListener _cachedAudioListener;
        private static float _nextAllowedAudioListenerQueryTime;
        private float _nextAllowedFootstepSoundTime;
        private Transform _leftFootTransform;
        private Transform _rightFootTransform;

        private void OnValidate()
        {
            audioListenerCullDistanceSqr = audioListenerCullDistance * audioListenerCullDistance;
            animator = animator ? animator : GetComponentInParent<Animator>();
        }

        private void Start()
        {
            if (!animator)
                animator = GetComponentInParent<Animator>();
        }

        public void PlayLeftFootstepSound()
        {
            if (IsCulled()) return;
            _leftFootTransform ??= animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            PlaySound(_leftFootTransform);
        }

        public void PlayRightFootstepSound()
        {
            if (IsCulled()) return;
            _rightFootTransform ??= animator.GetBoneTransform(HumanBodyBones.RightFoot);
            PlaySound(_rightFootTransform);
        }
        
        private void PlaySound(Transform ankleTransform)
        {
            var sound = GetSurfaceSound(ankleTransform);
            if (sound is null)
                return;
            var time = Time.time;
            if (time < _nextAllowedFootstepSoundTime)
                return;
            footstepAudioSource.pitch = Random.Range(minPitch, maxPitch);
            footstepAudioSource.PlayOneShot(sound);
            _nextAllowedFootstepSoundTime = time + minSoundInterval;
        }
        
        private AudioClip GetSurfaceSound(Transform ankleTransform)
        {
            if (!ankleTransform)
                return GetRandomDefaultFootstepSound();

            if (!FootstepMaterialDatabase.Query(
                    new Ray(ankleTransform.position + Vector3.up * 0.1f, Vector3.down),
                    out var record,
                    out _,
                    0.2f,
                    footstepLayerMask)) 
                return GetRandomDefaultFootstepSound();
            
            if (record.audioClips.Length == 0)
                return GetRandomDefaultFootstepSound();
                
            var audioClip = record.audioClips[Random.Range(0, record.audioClips.Length)];
            return audioClip ? audioClip : GetRandomDefaultFootstepSound();
        }
        
        private AudioClip GetRandomDefaultFootstepSound()
        {
            return defaultFootstepSounds.Length == 0
                ? null
                : defaultFootstepSounds[Random.Range(0, defaultFootstepSounds.Length)];
        }

        private bool IsCulled()
        {
            if (AudioListener.volume <= 0)
                return true;
            if (audioListenerCullDistanceSqr <= 0)
                return false;
            if (_nextAllowedAudioListenerQueryTime > Time.time)
                return true;
            if (!_cachedAudioListener)
                _cachedAudioListener = FindObjectOfType<AudioListener>();
            if (_cachedAudioListener)
                return (transform.position - _cachedAudioListener.transform.position).sqrMagnitude > audioListenerCullDistanceSqr;
            _nextAllowedAudioListenerQueryTime = Time.time + 1f;
            return true;
        }
    }
}
