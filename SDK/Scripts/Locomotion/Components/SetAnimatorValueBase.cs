using MetaverseCloudEngine.Unity.Avatar.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    /// <summary>
    /// A helper component for setting animator bool parameter values.
    /// </summary>
    [DefaultExecutionOrder(int.MaxValue)]
    public abstract class SetAnimatorValueBase<T> : TriInspectorMonoBehaviour
    {
        [SerializeField] private bool findAvatarContainer;
        [HideIf(nameof(findAvatarContainer))]
        [SerializeField] private Animator animator;
        [Required] 
        [SerializeField] private string parameterName;
        [SerializeField] private bool everyFrame;
        [SerializeField] private T value;

        protected int ParameterHash;

        /// <summary>
        /// The value to set the animator parameter to.
        /// </summary>
        public T Value { get => value; set => this.value = value; }

        /// <summary>
        /// Gets or sets the animator we're setting the bool on.
        /// </summary>
        public Animator Animator { get => animator; set => animator = value; }

        private void Awake()
        {
            if (!string.IsNullOrEmpty(parameterName))
                ParameterHash = Animator.StringToHash(parameterName);
        }

        private void Start()
        {
            if (findAvatarContainer)
                FindAvatarContainer();
        }

        private void LateUpdate()
        {
            if (everyFrame)
                SetValue();
        }

        /// <summary>
        /// Dereferences the animator.
        /// </summary>
        public void ClearAnimator()
        {
            animator = null;
        }

        /// <summary>
        /// Finds the nearest avatar container and updates the animator.
        /// </summary>
        public void FindAvatarContainer()
        {
            var container = GetComponentInParent<PlayerAvatarContainer>();
            if (!container) container = GetComponentInChildren<PlayerAvatarContainer>();
            if (container) animator = container.GetComponent<Animator>();
        }

        /// <summary>
        /// Sets the value of the parameter now.
        /// </summary>
        public void SetValue()
        {
            if (!this) return;
            if (ParameterHash == 0) return;
            if (!animator)
            {
                MetaverseProgram.Logger.LogWarning("Cannot set animator value because no animator is assigned. (GameObject: " + gameObject.name + ")");
                return;
            }
            if (!isActiveAndEnabled)
            {
                MetaverseProgram.Logger.LogWarning("Cannot set animator value when disabled. (GameObject: " + gameObject.name + ")");
                return;
            }
            SetInternal(Value);
        }

        /// <summary>
        /// Sets the value of the parameter now.
        /// </summary>
        /// <param name="v">The value to set.</param>
        public void SetValue(T v)
        {
            Value = v;
            SetValue();
        }

        protected abstract void SetInternal(T value);
    }
}