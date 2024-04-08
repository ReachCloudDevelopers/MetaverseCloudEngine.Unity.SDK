using System;
using Cinemachine;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.XR;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Cinemachine.Components.XR
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class CinemachineHmdTracking : NetworkObjectBehaviour
    {
        [Tooltip("If true, the brain camera will only be updated if the local player has input authority.")]
        [SerializeField] private bool requireAuthority = true;
        [SerializeField, Min(0.05f)] private float minimumNearClip = 0.15f;

        /// <summary>
        /// Gets or sets the minimum near clip plane for the brain camera.
        /// </summary>
        public float MinimumNearClip
        {
            get => minimumNearClip;
            set => minimumNearClip = value;
        }

        private void OnEnable()
        {
            Activate();
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void Activate()
        {
            if (requireAuthority && !IsInputAuthority) return;
            CinemachineHmdTrackingUpdater.TrackingInstance = this;
            CinemachineHmdTrackingUpdater.PerformUpdates = true;
        }

        private void Deactivate()
        {
            if (CinemachineHmdTrackingUpdater.TrackingInstance != this) return;
            CinemachineHmdTrackingUpdater.TrackingInstance = null;
            CinemachineHmdTrackingUpdater.PerformUpdates = false;
        }

        public override void OnLocalInputAuthority()
        {
            base.OnLocalInputAuthority();
            
            if (requireAuthority)
            {
                Activate();
            }
        }
        
        public override void OnRemoteInputAuthority()
        {
            base.OnRemoteInputAuthority();

            if (requireAuthority)
            {
                Deactivate();
            }
        }
    }

    [AddComponentMenu("")]
    [DefaultExecutionOrder(101)] // After everything else.
    public class CinemachineHmdTrackingUpdaterPostPass : CinemachineHmdTrackingUpdater
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            var go = new GameObject($"[{nameof(CinemachineHmdTrackingUpdaterPostPass)}]");
            go.AddComponent<CinemachineHmdTrackingUpdaterPostPass>();
            DontDestroyOnLoad(go);
            Application.onBeforeRender += UpdateInternal;
        }
    }

    [DefaultExecutionOrder(99)] // Before CinemachineBrain
    public class CinemachineHmdTrackingUpdaterPrePass : CinemachineHmdTrackingUpdater
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (!Application.isPlaying)
                return;
            
            var go = new GameObject($"[{nameof(CinemachineHmdTrackingUpdaterPrePass)}]");
            go.AddComponent<CinemachineHmdTrackingUpdaterPrePass>();
            go.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    public abstract class CinemachineHmdTrackingUpdater : MonoBehaviour
    {
        public static CinemachineHmdTracking TrackingInstance { get; internal set; }
        public static bool PerformUpdates { get; internal set; }

        private void Update() => UpdateInternal();

        private void FixedUpdate() => UpdateInternal();

        private void LateUpdate() => UpdateInternal();

        [BeforeRenderOrder(int.MaxValue)]
        protected static void UpdateInternal()
        {
            if (!PerformUpdates) return;
            if (!XRInputTrackingAPI.CurrentDevice.isValid) return;
            try
            {
                if (CinemachineCore.Instance is null) return;
                if (CinemachineCore.Instance.BrainCount == 0) return;
                var brain = CinemachineCore.Instance.GetActiveBrain(0);
                TrackingInstance.transform.GetPositionAndRotation(out var position, out var rotation);
                brain.transform.SetPositionAndRotation(position, rotation);
                if (!brain.OutputCamera) return;
                brain.OutputCamera.nearClipPlane = Mathf.Max(brain.OutputCamera.nearClipPlane, TrackingInstance.MinimumNearClip);
                brain.OutputCamera.transform.SetPositionAndRotation(position, rotation);
            }
            catch (Exception e)
            {
                TrackingInstance = null;
                PerformUpdates = false;
                MetaverseProgram.Logger.Log($"[{nameof(CinemachineHmdTrackingUpdater)}] {e}");
            }
        }
    }
}