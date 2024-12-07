using System.Linq;
using MetaverseCloudEngine.Unity.Locomotion.Components;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles.Land.Networking
{
    [RequireComponent(typeof(VehicleParent))]
    [RequireComponent(typeof(NetworkTransform))]
    [DisallowMultipleComponent]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Vehicles/Land/Networking/Vehicles - Vehicle Parent Networking")]
    public partial class VehicleParentNetworking : NetworkObjectBehaviour
    {
#if MV_XR_TOOLKIT
        [SerializeField] private Seat driverSeat;
#else
        [InfoBox("Please install the XR Toolkit package to use this component.")]
#endif
        [SerializeField] private bool allowUserControl = true;

        private BasicInput _input;
        private FollowAI _followAI;

        public bool AllowUserControl
        {
            get => allowUserControl;
            set
            {
                allowUserControl = value;
                SetControl(IsStateAuthority);
            }
        }

        private void Reset()
        {
            GetComponent<NetworkTransform>().synchronizationOptions =
                NetworkTransform.SyncOptions.Position |
                NetworkTransform.SyncOptions.Rotation |
                NetworkTransform.SyncOptions.Parent;

            if (!GetComponents<NetworkObject>().Any())
                gameObject.AddComponent<NetworkObject>();
        }

        protected override void Awake()
        {
            _input = GetComponent<BasicInput>();
            _followAI = GetComponent<FollowAI>();

            SetControl(false);

#if MV_XR_TOOLKIT
            if (driverSeat != null)
            {
                OnSeatEntered(!driverSeat || driverSeat.CurrentSitter != null);
                driverSeat.events.onEnteredValue.AddListener(OnSeatEntered);
            }
#endif

            base.Awake();
        }

        public override void OnLocalStateAuthority() => SetControl(true);
        public override void OnRemoteStateAuthority() => SetControl(false);

        private void SetControl(bool isControlEnabled)
        {
            if (_input)
            {
                _input.enabled = isControlEnabled && allowUserControl;
                if (IsStateAuthority || !NetworkObject)
                {
                    GasMotor gasMotor = GetComponentInChildren<GasMotor>(true);
                    if (gasMotor) 
                        gasMotor.ignition = CanActivateIgnition() && isControlEnabled;

                    if (!isControlEnabled || !allowUserControl)
                        StopVehicle();
                }
            }
            if (_followAI) _followAI.enabled = isControlEnabled;
        }

        private void StopVehicle()
        {
            VehicleParent vehicleParent = GetComponent<VehicleParent>();
            vehicleParent.SetBrake(1);
            vehicleParent.SetEbrake(1);
            vehicleParent.SetAccel(0);
            vehicleParent.SetSteer(0);
            vehicleParent.SetBoost(false);

            if (IsHoverMotor()) 
                vehicleParent.brakeIsReverse = false;
        }

        private void OnSeatEntered(bool value)
        {
            AllowUserControl = value;
        }

        private bool IsHoverMotor() => GetComponentInChildren<HoverMotor>(true);
        
        private bool CanActivateIgnition()
        {
            return allowUserControl
#if MV_XR_TOOLKIT
                   && (driverSeat == null || (driverSeat.CurrentSitter != null && driverSeat.CurrentSitter.IsInputAuthority));
#else
                ;
#endif
        }
    }
}