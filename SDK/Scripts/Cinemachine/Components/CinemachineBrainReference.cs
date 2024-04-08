using Cinemachine;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Cinemachine.Components
{
    [HideMonoScript]
    [DefaultExecutionOrder(int.MaxValue)]
    public class CinemachineBrainReference : TriInspectorMonoBehaviour
    {
        [Header("Tracking")]
        [Required] [SerializeField] private Transform follower;
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool followRotation = true;
        
        [Header("Events")]
        public UnityEvent<CinemachineBrain> onBrainFound;
        public UnityEvent<Camera> onCameraFound;
        public UnityEvent onBrainLost;

        [Header("Update Methods")]
        public bool update;
        public bool fixedUpdate;
        public bool lateUpdate = true;
        
        private CinemachineBrain _brain;

        public bool FollowPosition
        {
            get => followPosition;
            set => followPosition = value;
        }

        public bool FollowRotation
        {
            get => followRotation;
            set => followRotation = value;
        }

        public CinemachineBrain Brain => _brain;

        private void OnValidate()
        {
            if (!follower)
                follower = transform;
        }

        private void OnEnable()
        {
            follower = follower ? follower : transform;
            CinemachineCore.CameraCutEvent.AddListener(OnCinemachineCameraUpdate);
            CinemachineBrainUpdate();
        }

        private void OnDisable()
        {
            CinemachineCore.CameraCutEvent.RemoveListener(OnCinemachineCameraUpdate);
        }

        private void FixedUpdate()
        {
            if (fixedUpdate)
                BrainUpdate(_brain);
        }
        
        private void Update()
        {
            if (update)
                BrainUpdate(_brain);
        }

        private void LateUpdate()
        {
            if (lateUpdate)
                BrainUpdate(_brain);
        }

        private void CinemachineBrainUpdate()
        {
            if (CinemachineCore.Instance.BrainCount == 0)
            {
                if (_brain != null)
                {
                    onBrainLost?.Invoke();
                    _brain = null;
                }

                return;
            }

            CheckBrainChanged(CinemachineCore.Instance.GetActiveBrain(0));
        }

        private void BrainUpdate(CinemachineBrain brain)
        {
            if (!followPosition && !followRotation) return;
            if (brain)
            {
                var brainT = brain.transform;
                if (!follower) return;
                if (followPosition) follower.position = brainT.position;
                if (followRotation) follower.rotation = brainT.rotation;
            }
            else if (CinemachineCore.Instance != null && CinemachineCore.Instance.BrainCount > 0)
            {
                CheckBrainChanged(CinemachineCore.Instance.GetActiveBrain(0));
            }
        }

        private void OnCinemachineCameraUpdate(CinemachineBrain brain)
        {
            CinemachineBrainUpdate();
        }

        private void CheckBrainChanged(CinemachineBrain brain)
        {
            if (_brain == brain)
                return;
            
            if (_brain)
            {
                _brain = null;
                onBrainLost?.Invoke();
            }

            _brain = brain;

            if (!brain)
                return;
            
            onBrainFound?.Invoke(brain);
            if (brain.OutputCamera)
                onCameraFound?.Invoke(brain.OutputCamera);
        }
    }
}
