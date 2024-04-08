using System;
using MetaverseCloudEngine.Unity.Avatar.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [HideMonoScript]
    public abstract class VrIkSystemBase : NetworkObjectBehaviour, IVrIkSystem
    {
        #region Inspector

        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private Animator avatar;
        [SerializeField] private VrTrackers trackers;
        [SerializeField] private Transform rootTransform;

        #endregion

        #region Fields

        protected Transform Transform;
        private string _calibrationJson;

        #endregion

        #region Properties

        public bool HasCalibrationData => !string.IsNullOrEmpty(_calibrationJson);
        public Animator Avatar => avatar;
        public VrTrackers Trackers => trackers;

        public Transform RootTransform
        {
            get => rootTransform ? rootTransform : Transform;
            set => rootTransform = value;
        }

        public event VrIkSystemCalibratedDelegate Calibrated;

        #endregion

        #region Unity Events

        protected override void Awake()
        {
            Transform = transform;
            trackers.TrackersChanged += OnTrackersChanged;
            Calibrated += OnCalibrated;

            base.Awake();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            trackers.TrackersChanged -= OnTrackersChanged;
            Calibrated -= OnCalibrated;
        }

        #endregion

        #region Protected Methods

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            if (!NetworkObject)
                Initialize();
        }

        public override void OnNetworkReady(bool offline)
        {
            if (!offline)
                NetworkObject.InvokeRPC((short) NetworkRpcType.VrIkRequestCalibrationData, NetworkObject.StateAuthorityID, NetworkObject.Networking.LocalPlayerID);
        }

        protected override void RegisterNetworkRPCs()
        {
            NetworkObject.RegisterRPC((short) NetworkRpcType.VrIkCalibrate, RPC_VRIKCalibrate);
            NetworkObject.RegisterRPC((short) NetworkRpcType.VrIkRequestCalibrationData, RPC_RequestCalibrationData);
            Initialize();
        }

        protected override void UnRegisterNetworkRPCs()
        {
            NetworkObject.UnregisterRPC((short) NetworkRpcType.VrIkCalibrate, RPC_VRIKCalibrate);
            NetworkObject.UnregisterRPC((short) NetworkRpcType.VrIkRequestCalibrationData, RPC_RequestCalibrationData);
        }

        #endregion

        #region Public Methods

        public virtual void UpdateTrackers(VrTrackers vrt)
        {
            trackers = vrt;
            if (HasCalibrationData)
                Calibrate();
        }

        public virtual void UpdateAvatar(Animator a)
        {
            avatar = a;
            if (HasCalibrationData)
                Calibrate();
        }

        public virtual void SetIKActive(bool active)
        {
            enabled = active;
        }

        public virtual void Destroy()
        {
            Destroy(gameObject);
        }

        public void Calibrate(VrIkSystemCalibratedDelegate onCalibrated = null, Action onFailed = null)
        {
            if (!IsStateAuthority)
            {
                if (HasCalibrationData)
                    Calibrate(_calibrationJson, onFailed);
                return;
            }

            _calibrationJson = null;
            CalibrateInternal(Calibrated, onFailed);
        }

        public void Calibrate(string calibrationJson, Action onCalibrated = null, Action onFailed = null)
        {
            if (!avatar)
            {
                _calibrationJson = calibrationJson;
                return;
            }

            _calibrationJson = null;
            CalibrateInternal(calibrationJson, () => Calibrated?.Invoke(calibrationJson), onFailed);
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            if (autoInitialize && avatar && IsStateAuthority)
                Calibrate();
        }

        private void SendCalibrationDataToPlayer(int playerID)
        {
            if (!string.IsNullOrEmpty(_calibrationJson) && IsStateAuthority)
                NetworkObject.InvokeRPC((short) NetworkRpcType.VrIkCalibrate, playerID, _calibrationJson);
        }

        private void OnTrackersChanged()
        {
            if (!Avatar)
                return;

            if (!string.IsNullOrEmpty(_calibrationJson))
                Calibrate(_calibrationJson);
            else
                Calibrate();
        }

        private void RPC_VRIKCalibrate(short procedureID, int sendingPlayer, object content)
        {
            if (content is not string jsonData || string.IsNullOrEmpty(jsonData)) return;
            Calibrate(jsonData);
        }

        private void RPC_RequestCalibrationData(short procedureID, int sendingPlayer, object content)
        {
            if (content is not int playerID) return;
            SendCalibrationDataToPlayer(playerID);
        }

        private void OnCalibrated(string calibrationDataJson)
        {
            _calibrationJson = calibrationDataJson;
            if (IsStateAuthority && NetworkObject && !string.IsNullOrEmpty(calibrationDataJson))
                NetworkObject.InvokeRPC((short) NetworkRpcType.VrIkCalibrate, NetworkMessageReceivers.Others,
                    calibrationDataJson);
        }

        #endregion

        #region Protected Methods

        protected abstract void CalibrateInternal(VrIkSystemCalibratedDelegate onCalibrated = null,
            Action onFailed = null);

        protected abstract void CalibrateInternal(string calibrationJson, Action onCalibrated = null,
            Action onFailed = null);

        #endregion
    }
}