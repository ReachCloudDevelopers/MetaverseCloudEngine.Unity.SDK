using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using UnityEngine;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [RequireComponent(typeof(Animator))]
    public class XRHandPoseAnimator : NetworkObjectBehaviour
    {
        private const int MaximumNetUpdatesPerSecond = 4;

        [System.Flags]
        public enum HandPoseFlags : byte
        {
            Trigger = 1,
            Grip = 2,
            Thumb = 4,
            IsTracked = 8,
        }

        [System.Serializable]
        private class HandAnimation
        {
            public string thumbParam;
            public string triggerParam;
            public string gripParam;
            public string isTrackedParam;

            private int _thumbHash;

            public int ThumbHash
            {
                get
                {
                    if (_thumbHash == 0)
                        _thumbHash = Animator.StringToHash(thumbParam);
                    return _thumbHash;
                }
            }

            private int _triggerHash;

            public int TriggerHash
            {
                get
                {
                    if (_triggerHash == 0)
                        _triggerHash = Animator.StringToHash(triggerParam);
                    return _triggerHash;
                }
            }

            private int _gripHash;

            public int GripHash
            {
                get
                {
                    if (_gripHash == 0)
                        _gripHash = Animator.StringToHash(gripParam);
                    return _gripHash;
                }
            }

            private int _isTrackedHash;

            public int IsTrackedHash
            {
                get
                {
                    if (_isTrackedHash == 0)
                        _isTrackedHash = Animator.StringToHash(isTrackedParam);
                    return _isTrackedHash;
                }
            }
        }

        [SerializeField] private HandAnimation lHandAnimations = new()
        {
            thumbParam = "ThumbL",
            triggerParam = "TriggerL",
            gripParam = "GripL",
            isTrackedParam = "LHandTracked",
        };

        [SerializeField] private HandAnimation rHandAnimations = new()
        {
            thumbParam = "ThumbR",
            triggerParam = "TriggerR",
            gripParam = "GripR",
            isTrackedParam = "RHandTracked",
        };

        private Animator _animator;

        private HandPoseFlags _oldLFlags;
        private HandPoseFlags _lFlags;

        private HandPoseFlags _oldRFlags;
        private HandPoseFlags _rFlags;

        private float _nextUpdateTime;

        public float SimulateGripStrengthL { get; set; }
        public float SimulateTriggerStrengthL { get; set; }
        public bool SimulateThumbL { get; set; }
        public bool SimulateIsTrackedL { get; set; }

        public float SimulateGripStrengthR { get; set; }
        public float SimulateTriggerStrengthR { get; set; }
        public bool SimulateThumbR { get; set; }
        public bool SimulateIsTrackedR { get; set; }

        protected override void Awake()
        {
            _animator = GetComponent<Animator>();
            
            base.Awake();
        }

        protected override void RegisterNetworkRPCs()
        {
            NetworkObject.RegisterRPC((short) NetworkRpcType.XRHandPoseFlags, RPC_XRHandPoseFlags);
        }

        protected override void UnRegisterNetworkRPCs()
        {
            NetworkObject.UnregisterRPC((short) NetworkRpcType.XRHandPoseFlags, RPC_XRHandPoseFlags);
        }

        private void Update() => AnimationUpdate();

        private void FixedUpdate() => NetworkUpdate();

        private void AnimationUpdate()
        {
            _lFlags = AnimateDevice(
                InputDevices.GetDeviceAtXRNode(XRNode.LeftHand),
                _lFlags,
                lHandAnimations.TriggerHash,
                lHandAnimations.ThumbHash,
                lHandAnimations.GripHash,
                lHandAnimations.IsTrackedHash,
                SimulateTriggerStrengthL,
                SimulateThumbL,
                SimulateGripStrengthL,
                SimulateIsTrackedL);

            _rFlags = AnimateDevice(
                InputDevices.GetDeviceAtXRNode(XRNode.RightHand),
                _rFlags,
                rHandAnimations.TriggerHash,
                rHandAnimations.ThumbHash,
                rHandAnimations.GripHash,
                rHandAnimations.IsTrackedHash,
                SimulateTriggerStrengthR,
                SimulateThumbR,
                SimulateGripStrengthR,
                SimulateIsTrackedR);
        }

        private void NetworkUpdate()
        {
            if (!NetworkObject ||
                NetworkObject.Networking == null ||
                NetworkObject.Networking.IsOfflineMode)
                return;

            if (!(Time.fixedTime > _nextUpdateTime) || !IsStateAuthority)
                return;

            if (_oldRFlags != _rFlags || _oldLFlags != _lFlags)
                NetworkSend();

            _oldLFlags = _lFlags;
            _oldRFlags = _rFlags;
            _nextUpdateTime = Time.time + 1f / MaximumNetUpdatesPerSecond;
        }

        private void NetworkSend() => NetworkObject.InvokeRPC((short) NetworkRpcType.XRHandPoseFlags,
            NetworkMessageReceivers.Others, new object[] {_lFlags, _rFlags});

        private HandPoseFlags AnimateDevice(
            InputDevice device,
            HandPoseFlags netFlags,
            int triggerHash, int thumbHash, int gripHash, int isTrackedHash,
            float simulateTrigger, bool simulateThumb, float simulateGrip, bool simulateTracked)
        {
            if (!IsStateAuthority)
            {
                _animator.SetFloat(thumbHash, netFlags.HasFlag(HandPoseFlags.Thumb) ? 1 : 0, 0.1f, Time.deltaTime);
                _animator.SetFloat(triggerHash, netFlags.HasFlag(HandPoseFlags.Trigger) ? 1 : 0, 0.1f, Time.deltaTime);
                _animator.SetFloat(gripHash, netFlags.HasFlag(HandPoseFlags.Grip) ? 1 : 0, 0.1f, Time.deltaTime);
                _animator.SetBool(isTrackedHash, netFlags.HasFlag(HandPoseFlags.IsTracked));
                return netFlags;
            }

            bool isTracked = false;
            if (device.isValid)
                device.TryGetFeatureValue(CommonUsages.isTracked, out isTracked);

            if (!isTracked)
            {
                _animator.SetFloat(thumbHash, simulateThumb ? 1 : 0);
                _animator.SetFloat(triggerHash, simulateTrigger);
                _animator.SetFloat(gripHash, simulateGrip);
                _animator.SetBool(isTrackedHash, simulateTracked);
                return CalculateFlags(simulateTrigger, simulateThumb, simulateGrip, simulateTracked);
            }

            _animator.SetBool(isTrackedHash, true);

            if (!device.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out bool thumbTouch))
                thumbTouch = simulateThumb;

            _animator.SetFloat(thumbHash, thumbTouch ? 1f : 0f, 0.1f, Time.deltaTime);

            if (!device.TryGetFeatureValue(CommonUsages.trigger, out float trigger))
            {
                trigger = simulateTrigger;
                _animator.SetFloat(triggerHash, trigger, 0.1f, Time.deltaTime);
            }
            else
                _animator.SetFloat(triggerHash, trigger);

            if (!device.TryGetFeatureValue(CommonUsages.grip, out float grip))
            {
                grip = simulateGrip;
                _animator.SetFloat(gripHash, grip, 0.1f, Time.deltaTime);
            }
            else
                _animator.SetFloat(gripHash, grip);

            return CalculateFlags(trigger, thumbTouch, grip, isTracked);
        }

        private static HandPoseFlags CalculateFlags(float trigger, bool thumb, float grip, bool isTracked)
        {
            HandPoseFlags flags = 0;
            if (trigger > 0.9f) flags |= HandPoseFlags.Trigger;
            else if (trigger < 0.1f) flags &= ~HandPoseFlags.Trigger;
            if (grip > 0.9f) flags |= HandPoseFlags.Grip;
            else if (grip < 0.1f) flags &= ~HandPoseFlags.Grip;
            if (thumb) flags |= HandPoseFlags.Thumb;
            else if (thumb) flags &= ~HandPoseFlags.Thumb;
            if (isTracked) flags |= HandPoseFlags.IsTracked;
            else flags &= ~HandPoseFlags.IsTracked;
            return flags;
        }

        private void RPC_XRHandPoseFlags(short procedureID, int playerID, object content)
        {
            if (content is not object[] args || args.Length != 2)
                return;

            if (args[0] is not byte lHandPoseFlags || args[1] is not byte rHandPoseFlags)
                return;

            _lFlags = (HandPoseFlags) lHandPoseFlags;
            _rFlags = (HandPoseFlags) rHandPoseFlags;
        }
    }
}