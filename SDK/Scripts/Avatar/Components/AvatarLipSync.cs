using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    /// <summary>
    /// Adds the ability for avatars to perform lip synchronization with audio.
    /// </summary>
    [Experimental]
    [HideMonoScript]
    public partial class AvatarLipSync : TriInspectorMonoBehaviour
    {
        public enum Viseme
        {
            sil,
            PP,
            FF,
            TH,
            DD,
            kk,
            CH,
            SS,
            nn,
            RR,
            aa,
            E,
            ih,
            oh,
            ou
        }

        [System.Serializable]
        public class BlendShapeMapping
        {
            public string blendShapeName;
            public Viseme viseme;
            public SkinnedMeshRenderer[] renderers;
        }

        public BlendShapeMapping[] mappings;
        public PlayerAvatarContainer avatarContainer;
        public AudioSource audioSource;
    }
}