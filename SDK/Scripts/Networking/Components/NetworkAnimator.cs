using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public partial class NetworkAnimator : NetworkObjectBehaviour
    {
        private Animator _animator;

        /// <summary>
        /// The <see cref="Animator"/> attached to this object.
        /// </summary>
        public Animator animator {
            get {
                if (!_animator)
                    _animator = GetComponent<Animator>();
                return _animator;
            }
        }
    }
}