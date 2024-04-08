using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [HideMonoScript]
    [DefaultExecutionOrder(int.MaxValue)]
    public class UnityIkTarget : TriInspectorMonoBehaviour
    {
        [SerializeField] private AvatarIKGoal goal;
        [SerializeField] private Transform target;
        [SerializeField] private AvatarIKHint hint;
        [SerializeField] private Transform hintTarget;
        [SerializeField, Range(0, 1)] private float positionWeight = 1;
        [SerializeField, Range(0, 1)] private float rotationWeight = 1;
        [SerializeField] private int layerIndex;
        [SerializeField] private int order;
        [SerializeField] private Animator animator;

        private UnityAnimatorIKCallbacks _callbacks;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastHintTargetPosition;

        public AvatarIKGoal Goal { get => goal; set => goal = value; }
        public Transform Target { get => target; set => target = value; }
        public float Weight { get => positionWeight; set => positionWeight = value; }
        public int LayerIndex { get => layerIndex; set => layerIndex = value; }
        public Animator Animator {
            get => animator;
            set {
                if (_callbacks)
                {
                    _callbacks.UnRegisterIKCallback(OnAnimatorIKCallback);
                    _callbacks = null;
                }

                animator = value;

                if (animator)
                {
                    _callbacks = animator.gameObject.GetOrAddComponent<UnityAnimatorIKCallbacks>();
                    if (isActiveAndEnabled)
                        _callbacks.RegisterIKCallback(OnAnimatorIKCallback, order);
                }
            }
        }

        private void Awake()
        {
            if (animator)
                _callbacks = animator.gameObject.GetOrAddComponent<UnityAnimatorIKCallbacks>();
        }

        private void OnValidate()
        {
            if (!target)
                target = transform;
        }

        private void Reset()
        {
            animator = this.GetNearestComponent<Animator>();
        }

        private void OnEnable()
        {
            UpdatePose();
            
            if (_callbacks)
                _callbacks.RegisterIKCallback(OnAnimatorIKCallback, order);
        }

        private void OnDisable()
        {
            if (_callbacks)
                _callbacks.UnRegisterIKCallback(OnAnimatorIKCallback);
        }

        private void Update()
        {
            UpdatePose();
        }

        private void UpdatePose()
        {
            if (!target)
            {
                enabled = false;
                return;
            }

            _lastPosition = target.position;
            _lastRotation = target.rotation;

            if (hintTarget)
                _lastHintTargetPosition = hintTarget.position;
        }

        private void OnAnimatorIKCallback(int index)
        {
            if (!animator)
                enabled = false;

            if (index != LayerIndex)
                return;

            if (target)
            {
                animator.SetIKPosition(goal, _lastPosition);
                animator.SetIKRotation(goal, _lastRotation);
                animator.SetIKPositionWeight(goal, positionWeight);
                animator.SetIKRotationWeight(goal, rotationWeight);
            }

            if (hintTarget)
            {
                animator.SetIKHintPosition(hint, _lastHintTargetPosition);
                animator.SetIKHintPositionWeight(hint, positionWeight);
            }
        }
    }
}
