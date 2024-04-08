#if MV_XR_HANDS
using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Hands;

namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    /// Visualizes XR hands but with networking capabilities.
    /// </summary>
    [HideMonoScript]
    public class XRNetworkHandVisualizer : NetworkObjectBehaviour
    {
        /// <summary>
        /// The type of velocity to visualize.
        /// </summary>
        public enum VelocityTypeEnum
        {
            Linear,
            Angular,
            None,
        }

        [Tooltip("The parent NetworkTransform to attach the hands to.")]
        [SerializeField] [Required] private NetworkTransform parent;
        [Tooltip("The XROrigin to use for the hand transforms.")]
        [SerializeField] [Required] private XROrigin origin;
        [Tooltip("(Optional) The hand mesh to use for the left hand.")]
        [SerializeField] private NetworkObject leftHandMesh;
        [Tooltip("(Optional) The hand mesh to use for the right hand.")]
        [SerializeField] private NetworkObject rightHandMesh;
        [Tooltip("(Optional) The material to use for the hand meshes.")]
        [SerializeField] private Material handMeshMaterial;
        [Tooltip("True if the hand meshes should be drawn.")]
        [SerializeField] private bool drawMeshes = true;
        
        [Header("Debug")]
        [SerializeField] private GameObject debugDrawPrefab;
        [SerializeField] private bool debugDrawJoints;
        
        [Header("Velocity")]
        [SerializeField] private GameObject velocityPrefab;
        [SerializeField] private VelocityTypeEnum velocityType;

        private bool _previousDrawMeshes;
        private bool _previousDebugDrawJoints;

        private VelocityTypeEnum _previousVelocityType;
        private XRHandSubsystem _subsystem;
        private HandGameObjects _leftHandGameObjects;
        private HandGameObjects _rightHandGameObjects;
        private static readonly List<XRHandSubsystem> s_SubsystemsReuse = new();

        /// <summary>
        /// True if the hand meshes should be drawn.
        /// </summary>
        public bool DrawMeshes
        {
            get => drawMeshes;
            set => drawMeshes = value;
        }

        /// <summary>
        /// True if the hand joints should be drawn.
        /// </summary>
        public bool DebugDrawJoints
        {
            get => debugDrawJoints;
            set => debugDrawJoints = value;
        }

        /// <summary>
        /// The type of velocity to visualize.
        /// </summary>
        public VelocityTypeEnum VelocityType
        {
            get => velocityType;
            set => velocityType = value;
        }

        protected void OnEnable()
        {
            if (_subsystem == null)
                return;

            UpdateRenderingVisibility(_leftHandGameObjects, IsLeftHandTracking());
            UpdateRenderingVisibility(_rightHandGameObjects, IsRightHandTracking());
        }

        protected void OnDisable()
        {
            if (_subsystem != null)
            {
                _subsystem.trackingAcquired -= OnTrackingAcquired;
                _subsystem.trackingLost -= OnTrackingLost;
                _subsystem.updatedHands -= OnUpdatedHands;
                _subsystem = null;
            }

            UpdateRenderingVisibility(_leftHandGameObjects, false);
            UpdateRenderingVisibility(_rightHandGameObjects, false);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            
            if (!parent)
            {
                parent = GetComponentInParent<NetworkTransform>(true);
                if (!parent)
                    parent = gameObject.AddComponent<NetworkTransform>();
            }

            if (!origin)
            {
                origin = GetComponentInParent<XROrigin>(true);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (_leftHandGameObjects != null)
            {
                _leftHandGameObjects.OnDestroy();
                _leftHandGameObjects = null;
            }

            if (_rightHandGameObjects != null)
            {
                _rightHandGameObjects.OnDestroy();
                _rightHandGameObjects = null;
            }
        }

        protected void Update()
        {
            if (_subsystem != null)
                return;
            
            if (MetaSpace.Instance is not null && !MetaSpace.Instance.IsInitialized)
                return;

            if (NetworkObject && !NetworkObject.IsInputAuthority)
            {
                UpdateRenderingVisibility(_leftHandGameObjects, false);
                UpdateRenderingVisibility(_rightHandGameObjects, false);
                return;
            }

            SubsystemManager.GetSubsystems(s_SubsystemsReuse);
            if (s_SubsystemsReuse.Count == 0)
                return;

            _subsystem = s_SubsystemsReuse[0];

            if (_leftHandGameObjects == null && leftHandMesh != null)
            {
                _leftHandGameObjects = new HandGameObjects(
                    Handedness.Left,
                    transform,
                    leftHandMesh.gameObject,
                    handMeshMaterial,
                    debugDrawPrefab,
                    velocityPrefab);
            }

            if (_rightHandGameObjects == null && rightHandMesh != null)
            {
                _rightHandGameObjects = new HandGameObjects(
                    Handedness.Right,
                    transform,
                    rightHandMesh.gameObject,
                    handMeshMaterial,
                    debugDrawPrefab,
                    velocityPrefab);
            }

            UpdateRenderingVisibility(_leftHandGameObjects, IsLeftHandTracking());
            UpdateRenderingVisibility(_rightHandGameObjects, IsRightHandTracking());

            _previousDrawMeshes = drawMeshes;
            _previousDebugDrawJoints = debugDrawJoints;
            _previousVelocityType = velocityType;

            _subsystem.trackingAcquired += OnTrackingAcquired;
            _subsystem.trackingLost += OnTrackingLost;
            _subsystem.updatedHands += OnUpdatedHands;
        }

        private void UpdateRenderingVisibility(HandGameObjects handGameObjects, bool isTracked)
        {
            if (handGameObjects == null)
                return;

            handGameObjects.ToggleDrawMesh(drawMeshes && isTracked);
            handGameObjects.ToggleDebugDrawJoints(debugDrawJoints && isTracked);
            handGameObjects.SetVelocityType(isTracked ? velocityType : VelocityTypeEnum.None);
        }

        private void OnTrackingAcquired(XRHand hand)
        {
            switch (hand.handedness)
            {
                case Handedness.Left:
                    UpdateRenderingVisibility(_leftHandGameObjects, true);
                    break;
                case Handedness.Right:
                    UpdateRenderingVisibility(_rightHandGameObjects, true);
                    break;
            }
        }

        private void OnTrackingLost(XRHand hand)
        {
            switch (hand.handedness)
            {
                case Handedness.Left:
                    UpdateRenderingVisibility(_leftHandGameObjects, false);
                    break;
                case Handedness.Right:
                    UpdateRenderingVisibility(_rightHandGameObjects, false);
                    break;
            }
        }

        private void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
        {
            // We have no game logic depending on the Transforms, so early out here
            // (add game logic before this return here, directly querying from
            // subsystem.leftHand and subsystem.rightHand using GetJoint on each hand)
            if (updateType == XRHandSubsystem.UpdateType.Dynamic)
                return;

            var leftHandTracked = IsLeftHandTracking();
            var rightHandTracked = IsRightHandTracking();

            if (_previousDrawMeshes != drawMeshes)
            {
                _leftHandGameObjects.ToggleDrawMesh(drawMeshes && leftHandTracked);
                _rightHandGameObjects.ToggleDrawMesh(drawMeshes && rightHandTracked);
                _previousDrawMeshes = drawMeshes;
            }

            if (_previousDebugDrawJoints != debugDrawJoints)
            {
                _leftHandGameObjects.ToggleDebugDrawJoints(debugDrawJoints && leftHandTracked);
                _rightHandGameObjects.ToggleDebugDrawJoints(debugDrawJoints && rightHandTracked);
                _previousDebugDrawJoints = debugDrawJoints;
            }

            if (_previousVelocityType != velocityType)
            {
                _leftHandGameObjects.SetVelocityType(leftHandTracked ? velocityType : VelocityTypeEnum.None);
                _rightHandGameObjects.SetVelocityType(rightHandTracked ? velocityType : VelocityTypeEnum.None);
                _previousVelocityType = velocityType;
            }

            _leftHandGameObjects.UpdateJoints(
                origin,
                _subsystem.leftHand,
                (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0 && leftHandTracked,
                drawMeshes,
                debugDrawJoints,
                velocityType);

            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose) != 0)
                _leftHandGameObjects.UpdateRootPose(_subsystem.leftHand);

            _rightHandGameObjects.UpdateJoints(
                origin,
                _subsystem.rightHand,
                (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0 && rightHandTracked,
                drawMeshes,
                debugDrawJoints,
                velocityType);
            
            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose) != 0)
                _rightHandGameObjects.UpdateRootPose(_subsystem.rightHand);
        }

        private bool IsRightHandTracking()
        {
            return _subsystem.rightHand.isTracked && 
                   _subsystem.rightHand.rootPose.position.sqrMagnitude > 0;
        }

        private bool IsLeftHandTracking()
        {
            return _subsystem.leftHand.isTracked && 
                   _subsystem.leftHand.rootPose.position.sqrMagnitude > 0;
        }

        private class HandGameObjects
        {
            private GameObject _handRoot;
            private GameObject _drawJointsParent;

            private readonly Transform[] _jointXforms = new Transform[XRHandJointID.EndMarker.ToIndex()];
            private readonly GameObject[] _drawJoints = new GameObject[XRHandJointID.EndMarker.ToIndex()];
            private readonly GameObject[] _velocityParents = new GameObject[XRHandJointID.EndMarker.ToIndex()];
            private readonly LineRenderer[] _lines = new LineRenderer[XRHandJointID.EndMarker.ToIndex()];
            private bool _isTracked;

            private static readonly Vector3[] s_LinePointsReuse = new Vector3[2];
            private const float LineWidth = 0.005f;

            public HandGameObjects(
                Handedness handedness,
                Transform parent,
                GameObject meshPrefab,
                Material meshMaterial,
                GameObject debugDrawPrefab,
                GameObject velocityPrefab)
            {
                void AssignJoint(
                    XRHandJointID jointId,
                    Transform jointXForm,
                    Transform drawJointsParent)
                {
                    var jointIndex = jointId.ToIndex();
                    _jointXforms[jointIndex] = jointXForm;

                    _drawJoints[jointIndex] = Instantiate(debugDrawPrefab);
                    _drawJoints[jointIndex].transform.parent = drawJointsParent;
                    _drawJoints[jointIndex].name = jointId.ToString();

                    _velocityParents[jointIndex] = Instantiate(velocityPrefab);
                    _velocityParents[jointIndex].transform.parent = jointXForm;

                    _lines[jointIndex] = _drawJoints[jointIndex].GetComponent<LineRenderer>();
                    _lines[jointIndex].startWidth = _lines[jointIndex].endWidth = LineWidth;
                    s_LinePointsReuse[0] = s_LinePointsReuse[1] = jointXForm.position;
                    _lines[jointIndex].SetPositions(s_LinePointsReuse);
                }

                if (meshPrefab.TryGetComponent(out NetworkObject _) && MetaSpace.Instance)
                {
                    var networkingService = MetaSpace.Instance.GetService<IMetaSpaceNetworkingService>();
                    if (networkingService != null)
                    {
                        networkingService.SpawnGameObject(meshPrefab, no =>
                        {
                            if (!parent)
                            {
                                no.IsStale = true;
                                return;
                            }
                            
                            if (parent.TryGetComponent(out NetworkTransform _))
                                no.GameObject.transform.SetParent(parent);

                            OnHandRoot(no.GameObject);

                        }, parent.position, parent.rotation, false);
                        return;
                    }
                    else
                    {
                        _handRoot = Instantiate(meshPrefab, parent);
                        MetaverseProgram.Logger.Log("Failed to find network service. Cannot spawn networked hands.");
                    }
                }
                else
                {
                    _handRoot = Instantiate(meshPrefab, parent);
                }
                
                OnHandRoot(_handRoot);

                void OnHandRoot(GameObject root)
                {
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.identity;

                    Transform wristRootXForm = null;
                    for (var childIndex = 0; childIndex < root.transform.childCount; ++childIndex)
                    {
                        var child = root.transform.GetChild(childIndex);
                        if (child.gameObject.name.EndsWith(XRHandJointID.Wrist.ToString()))
                            wristRootXForm = child;
                        else if (child.gameObject.name.EndsWith("Hand") && meshMaterial != null &&
                                 child.TryGetComponent<SkinnedMeshRenderer>(out var renderer))
                            renderer.sharedMaterial = meshMaterial;
                    }

                    _drawJointsParent = new GameObject
                    {
                        transform =
                        {
                            parent = parent,
                            localPosition = Vector3.zero,
                            localRotation = Quaternion.identity
                        },
                        name = handedness + " Hand Debug Draw Joints"
                    };

                    if (wristRootXForm == null)
                    {
                        Debug.LogWarning("Hand transform hierarchy not set correctly - couldn't find Wrist joint!");
                    }
                    else
                    {
                        AssignJoint(XRHandJointID.Wrist, wristRootXForm, _drawJointsParent.transform);
                        for (var childIndex = 0; childIndex < wristRootXForm.childCount; ++childIndex)
                        {
                            var child = wristRootXForm.GetChild(childIndex);

                            if (child.name.EndsWith(XRHandJointID.Palm.ToString()))
                            {
                                AssignJoint(XRHandJointID.Palm, child, _drawJointsParent.transform);
                                continue;
                            }

                            for (var fingerIndex = (int)XRHandFingerID.Thumb;
                                 fingerIndex <= (int)XRHandFingerID.Little;
                                 ++fingerIndex)
                            {
                                var fingerId = (XRHandFingerID)fingerIndex;

                                var jointIdFront = fingerId.GetFrontJointID();
                                if (!child.name.EndsWith(jointIdFront.ToString()))
                                    continue;

                                AssignJoint(jointIdFront, child, _drawJointsParent.transform);
                                var lastChild = child;

                                var jointIndexBack = fingerId.GetBackJointID().ToIndex();
                                for (var jointIndex = jointIdFront.ToIndex() + 1;
                                     jointIndex <= jointIndexBack;
                                     ++jointIndex)
                                {
                                    for (var nextChildIndex = 0; nextChildIndex < lastChild.childCount; ++nextChildIndex)
                                    {
                                        var nextChild = lastChild.GetChild(nextChildIndex);
                                        if (nextChild.name.EndsWith(XRHandJointIDUtility.FromIndex(jointIndex).ToString()))
                                        {
                                            lastChild = nextChild;
                                            break;
                                        }
                                    }

                                    if (!lastChild.name.EndsWith(XRHandJointIDUtility.FromIndex(jointIndex).ToString()))
                                        throw new InvalidOperationException(
                                            "Hand transform hierarchy not set correctly - couldn't find " +
                                            XRHandJointIDUtility.FromIndex(jointIndex) + " joint!");

                                    var jointId = XRHandJointIDUtility.FromIndex(jointIndex);
                                    AssignJoint(jointId, lastChild, _drawJointsParent.transform);
                                }
                            }
                        }
                    }

                    for (var fingerIndex = (int)XRHandFingerID.Thumb;
                         fingerIndex <= (int)XRHandFingerID.Little;
                         ++fingerIndex)
                    {
                        var fingerId = (XRHandFingerID)fingerIndex;

                        var jointId = fingerId.GetFrontJointID();
                        if (_jointXforms[jointId.ToIndex()] == null)
                            Debug.LogWarning("Hand transform hierarchy not set correctly - couldn't find " + jointId + " joint!");
                    }
                }
            }

            public void OnDestroy()
            {
                if (_handRoot)
                    Destroy(_handRoot);
                _handRoot = null;

                for (var jointIndex = 0; jointIndex < _drawJoints.Length; ++jointIndex)
                {
                    if (_drawJoints[jointIndex])
                        Destroy(_drawJoints[jointIndex]);
                    _drawJoints[jointIndex] = null;
                }

                for (var jointIndex = 0; jointIndex < _velocityParents.Length; ++jointIndex)
                {
                    if (_velocityParents[jointIndex])
                        Destroy(_velocityParents[jointIndex]);
                    _velocityParents[jointIndex] = null;
                }

                if (_drawJointsParent)
                    Destroy(_drawJointsParent);
                _drawJointsParent = null;
            }

            public void ToggleDrawMesh(bool drawMesh)
            {
                for (var childIndex = 0; childIndex < _handRoot.transform.childCount; ++childIndex)
                {
                    var xForm = _handRoot.transform.GetChild(childIndex);
                    if (xForm.TryGetComponent<SkinnedMeshRenderer>(out var renderer))
                        renderer.enabled = drawMesh;
                }
            }

            public void ToggleDebugDrawJoints(bool debugDrawJoints)
            {
                for (var jointIndex = 0; jointIndex < _drawJoints.Length; ++jointIndex)
                {
                    ToggleRenderers<MeshRenderer>(debugDrawJoints, _drawJoints[jointIndex].transform);
                    _lines[jointIndex].enabled = debugDrawJoints;
                }

                _lines[0].enabled = false;
            }

            public void SetVelocityType(VelocityTypeEnum velocityType)
            {
                foreach (var tr in _velocityParents)
                    ToggleRenderers<LineRenderer>(velocityType != VelocityTypeEnum.None, tr.transform);
            }

            public void UpdateRootPose(XRHand hand)
            {
                var xForm = _jointXforms[XRHandJointID.Wrist.ToIndex()];
                xForm.localPosition = hand.rootPose.position;
                xForm.localRotation = hand.rootPose.rotation;
            }

            public void UpdateJoints(
                XROrigin xrOrigin,
                XRHand hand,
                bool areJointsTracked,
                bool drawMeshes,
                bool debugDrawJoints,
                VelocityTypeEnum velocityType)
            {
                if (_isTracked != areJointsTracked)
                {
                    ToggleDrawMesh(areJointsTracked && drawMeshes);
                    ToggleDebugDrawJoints(areJointsTracked && debugDrawJoints);
                    SetVelocityType(areJointsTracked ? velocityType : VelocityTypeEnum.None);
                    _isTracked = areJointsTracked;
                }

                if (!_isTracked)
                    return;

                var originTransform = xrOrigin.Origin.transform;
                var originPose = new Pose(originTransform.position, originTransform.rotation);

                var wristPose = Pose.identity;
                UpdateJoint(debugDrawJoints, velocityType, originPose, hand.GetJoint(XRHandJointID.Wrist), ref wristPose);
                UpdateJoint(debugDrawJoints, velocityType, originPose, hand.GetJoint(XRHandJointID.Palm), ref wristPose, false);

                for (var fingerIndex = (int)XRHandFingerID.Thumb;
                    fingerIndex <= (int)XRHandFingerID.Little;
                    ++fingerIndex)
                {
                    var parentPose = wristPose;
                    var fingerId = (XRHandFingerID)fingerIndex;

                    var jointIndexBack = fingerId.GetBackJointID().ToIndex();
                    for (var jointIndex = fingerId.GetFrontJointID().ToIndex();
                        jointIndex <= jointIndexBack;
                        ++jointIndex)
                    {
                        if (_jointXforms[jointIndex] != null)
                            UpdateJoint(debugDrawJoints, velocityType, originPose, hand.GetJoint(XRHandJointIDUtility.FromIndex(jointIndex)), ref parentPose);
                    }
                }
            }

            private void UpdateJoint(
                bool debugDrawJoints,
                VelocityTypeEnum velocityType,
                Pose originPose,
                XRHandJoint joint,
                ref Pose parentPose,
                bool cacheParentPose = true)
            {
                var jointIndex = joint.id.ToIndex();
                var xForm = _jointXforms[jointIndex];
                if (xForm == null || !joint.TryGetPose(out var pose))
                    return;

                _drawJoints[jointIndex].transform.localPosition = pose.position;
                _drawJoints[jointIndex].transform.localRotation = pose.rotation;

                if (debugDrawJoints && joint.id != XRHandJointID.Wrist)
                {
                    s_LinePointsReuse[0] = parentPose.GetTransformedBy(originPose).position;
                    s_LinePointsReuse[1] = pose.GetTransformedBy(originPose).position;
                    _lines[jointIndex].SetPositions(s_LinePointsReuse);
                }

                var inverseParentRotation = Quaternion.Inverse(parentPose.rotation);
                xForm.localPosition = inverseParentRotation * (pose.position - parentPose.position);
                xForm.localRotation = inverseParentRotation * pose.rotation;
                if (cacheParentPose)
                    parentPose = pose;

                if (velocityType == VelocityTypeEnum.None || !_velocityParents[jointIndex].TryGetComponent<LineRenderer>(out var renderer))
                    return;
                
                _velocityParents[jointIndex].transform.localPosition = Vector3.zero;
                _velocityParents[jointIndex].transform.localRotation = Quaternion.identity;

                s_LinePointsReuse[0] = s_LinePointsReuse[1] = _velocityParents[jointIndex].transform.position;
                switch (velocityType)
                {
                    case VelocityTypeEnum.Linear:
                    {
                        if (joint.TryGetLinearVelocity(out var velocity))
                            s_LinePointsReuse[1] += velocity;
                        break;
                    }
                    case VelocityTypeEnum.Angular:
                    {
                        if (joint.TryGetAngularVelocity(out var velocity))
                            s_LinePointsReuse[1] += 0.05f * velocity.normalized;
                        break;
                    }
                }

                renderer.SetPositions(s_LinePointsReuse);
            }

            private static void ToggleRenderers<TRenderer>(bool toggle, Transform xForm)
                where TRenderer : Renderer
            {
                if (xForm.TryGetComponent<TRenderer>(out var renderer))
                    renderer.enabled = toggle;

                for (var childIndex = 0; childIndex < xForm.childCount; ++childIndex)
                    ToggleRenderers<TRenderer>(toggle, xForm.GetChild(childIndex));
            }
        }
    }
}
#endif