using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    /// <summary>
    /// A helper component that allows you to play random sounds from
    /// a set of audio clips.
    /// </summary>
    [Experimental]
    [HideMonoScript]
    public class PlaySound : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The audio clip selection mode.
        /// </summary>
        public enum SelectionMode
        {
            /// <summary>
            /// Pick a sound at random.
            /// </summary>
            Random,
            /// <summary>
            /// Pick a sound ordered by the index.
            /// </summary>
            Ordered,
        }

        [InfoBox("Call Play() on this component in order to play a sound from the specified clips.")]
        [Tooltip("The clips to play.")]
        [SerializeField] private AudioClip[] clips;
        [Tooltip("The selection mode to use for selecting from the audio clips.")]
        [SerializeField] private SelectionMode selectionMode = SelectionMode.Random;
        [Tooltip("Specify the volume of the audio.")]
        [SerializeField, Range(0, 1f)] private float volume = 1f;
        [SerializeField, Range(0, 2f)] private float maxPitch = 1f;
        [SerializeField, Range(0, 2f)] private float minPitch = 1f;
        [Tooltip("(Optional) The audio source to play the sound from.")]
        [SerializeField] private AudioSource source;
        [Tooltip("Specify if the audio source is a prefab, if so it will be instantiated to play the clip.")]
        [SerializeField] private bool isSourcePrefab;

        private int _orderIndex;
        private ObjectPool<AudioSource> _dynamicAudioSourcePool;
        private ObjectPool<AudioSource> _prefabAudioSourcePool;

        private void OnDestroy()
        {
            _dynamicAudioSourcePool?.Clear();
            _dynamicAudioSourcePool?.Dispose();
            _prefabAudioSourcePool?.Clear();
            _prefabAudioSourcePool?.Dispose();
        }

        /// <summary>
        /// Plays a random sound.
        /// </summary>
        public void Play()
        {
            if (!this)
                return;
            
            if (!isActiveAndEnabled) 
                return;

            var clip = NextClip();
            if (clip == null)
                return;

            var tr = transform;
            if (source)
            {
                if (isSourcePrefab)
                {
                    _prefabAudioSourcePool ??= new ObjectPool<AudioSource>(
                        createFunc: () => Instantiate(source.gameObject).GetComponent<AudioSource>().Do(a => a.hideFlags = HideFlags.HideInHierarchy),
                        actionOnGet: s => { if (s) s.gameObject.SetActive(true); },
                        actionOnRelease: s => { if (s) s.gameObject.SetActive(false); },
                        actionOnDestroy: s => { if (s) Destroy(s.gameObject); });
                    
                    var sourceInstance = _prefabAudioSourcePool.Get();
                    sourceInstance.transform.SetPositionAndRotation(tr.position, tr.rotation);
                    sourceInstance.clip = clip;
                    sourceInstance.Play();
                    MetaverseDispatcher.WaitForSeconds(clip.length + 0.1f, () => _prefabAudioSourcePool.Release(sourceInstance));
                }
                else
                {
                    source.PlayOneShot(clip, volume);
                }
            }
            else
            {
                _dynamicAudioSourcePool ??= new ObjectPool<AudioSource>(
                    createFunc: () =>
                    {
                        var o = new GameObject("AudioSource")
                        {
                            hideFlags = HideFlags.HideInHierarchy
                        };
                        return o.AddComponent<AudioSource>();
                    },
                    actionOnGet: s => { if (s) s.gameObject.SetActive(true); },
                    actionOnRelease: s => { if (s) s.gameObject.SetActive(false); },
                    actionOnDestroy: s => { if (s) Destroy(s.gameObject); });

                var sourceInstance = _dynamicAudioSourcePool.Get();
                sourceInstance.transform.position = tr.position;
                sourceInstance.clip = clip;
                sourceInstance.volume = volume;
                sourceInstance.pitch = Random.Range(minPitch, maxPitch);
                sourceInstance.Play();
                MetaverseDispatcher.WaitForSeconds(clip.length + 0.1f, () => _dynamicAudioSourcePool.Release(sourceInstance));
            }
        }

        private AudioClip NextClip()
        {
            try
            {
                if (clips.Length == 0)
                    return null;

                return selectionMode switch
                {
                    SelectionMode.Ordered => clips[_orderIndex % clips.Length],
                    SelectionMode.Random => clips[Random.Range(0, clips.Length)],
                    _ => null,
                };
            }
            finally
            {
                _orderIndex++;
            }
        }
    }
}
