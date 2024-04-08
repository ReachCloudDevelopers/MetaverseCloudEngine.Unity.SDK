using System;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Physix.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [HideMonoScript]
    [DefaultExecutionOrder(-int.MaxValue + 1)]
    [Experimental]
    public class PhysicsRenderInterpolation : TriInspectorMonoBehaviour
    {
        [Required] [SerializeField] private Rigidbody body;
        [Required] [SerializeField] private Transform renderNode;
        
        [Header("Position")]
        [SerializeField] private bool enablePosition = true;
        [Tooltip("This is the maximum distance the render node can be from the fixed node before it is teleported." +
                 "This can mitigate jittering whenever the physics object is moving a lot.")]
        [SerializeField, Range(0, 0.25f)]
        private float positionChangeThreshold;
        [Tooltip("If a position change threshold is set, this is the speed at which the render node will interpolate to the fixed node " +
                 "if the distance between them is less than the position change threshold. This is to prevent discrepancies between the " +
                 "render node and the fixed node.")]
        [SerializeField, Range(0, 15f)]
        private float positionInterpolationSpeed = 10f;
        [Tooltip("If this is enabled, the render node will only interpolate vertically. This can be useful for objects that are " +
                 "moving horizontally very fast, but not vertically, or for VR where the player's head modifies the position of the " +
                 "body.")]
        [SerializeField] 
        private bool onlyApplyVerticalPositionInterpolation;

        [Header("Rotation")]
        [SerializeField] private bool enableRotation = true;
        [Tooltip("This is the maximum angle the render node can be from the fixed node before it is teleported." +
                 "This can mitigate jittering whenever the physics object is rotating a lot.")]
        [SerializeField, Range(0, 25f)]
        private float rotationChangeThreshold;
        [Tooltip("If a rotation change threshold is set, this is the speed at which the render node will interpolate to the fixed node " +
                 "if the angle between them is less than the rotation change threshold. This is to prevent discrepancies between the " +
                 "render node and the fixed node.")]
        [SerializeField, Range(0, 15f)] 
        private float rotationInterpolationSpeed = 10f;
        [SerializeField]
        [Tooltip("Allows the render node to interpolate even if the body is within a parent. You may " +
                 "want to disable this if the body is a child of a moving object.")]
        private bool allowInterpolationWithinBodyParent;
        
        private Vector3 _prevFixedPosition;
        private Quaternion _prevFixedRotation;
        private Vector3 _fixedPosition;
        private Quaternion _fixedRotation;
        private Vector3 _renderPosition;
        private Quaternion _renderRotation;
        private bool _wasInterpolating;
        
        public bool EnableRenderThreshold { get; set; } = true;
        
        public float PositionChangeThreshold
        {
            get => positionChangeThreshold;
            set => positionChangeThreshold = value;
        }
        
        public float RotationChangeThreshold
        {
            get => rotationChangeThreshold;
            set => rotationChangeThreshold = value;
        }
        
        public bool OnlyApplyVerticalPositionInterpolation
        {
            get => onlyApplyVerticalPositionInterpolation;
            set => onlyApplyVerticalPositionInterpolation = value;
        }

        private void OnValidate()
        {
            if (body == null)
                body = GetComponentInParent<Rigidbody>();
            if (renderNode == null && body.transform != transform)
                renderNode = transform;
            if (body.transform == renderNode)
                renderNode = null;
        }

        private void OnEnable()
        {
            FloatingOrigin.Shifted += OnFloatingOriginShifted;
            Teleport();
        }

        private void OnDisable()
        {
            FloatingOrigin.Shifted -= OnFloatingOriginShifted;
            if (!renderNode) return;
            if (renderNode.parent)
            {
                renderNode.localRotation = Quaternion.identity;
                renderNode.localPosition = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            var t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            var targetRenderPosition = Vector3.Lerp(_prevFixedPosition, _fixedPosition, t);
            var targetRenderRotation = Quaternion.Slerp(_prevFixedRotation, _fixedRotation, t);

            if (EnableRenderThreshold)
            {
                if (positionChangeThreshold > 0)
                    if (Vector3.Distance(targetRenderPosition, _renderPosition) > positionChangeThreshold)
                        _renderPosition = Vector3.MoveTowards(_renderPosition, targetRenderPosition,
                            Vector3.Distance(_renderPosition, targetRenderPosition) - positionChangeThreshold);
                    else
                        _renderPosition = Vector3.Lerp(_renderPosition, targetRenderPosition,
                            1f - Mathf.Exp(-positionInterpolationSpeed * Time.deltaTime));
                else
                    _renderPosition = targetRenderPosition;

                if (onlyApplyVerticalPositionInterpolation)
                {
                    var renderPositionRelativeToOrientation = Quaternion.Inverse(targetRenderRotation) * _renderPosition;
                    var targetPositionRelativeToOrientation = Quaternion.Inverse(targetRenderRotation) * targetRenderPosition;
                    renderPositionRelativeToOrientation.x = targetPositionRelativeToOrientation.x;
                    renderPositionRelativeToOrientation.z = targetPositionRelativeToOrientation.z;
                    _renderPosition = targetRenderRotation * renderPositionRelativeToOrientation;
                }
                
                if (rotationChangeThreshold > 0)
                    if (Quaternion.Angle(targetRenderRotation, _renderRotation) > rotationChangeThreshold)
                        _renderRotation = Quaternion.RotateTowards(_renderRotation, targetRenderRotation,
                            Quaternion.Angle(_renderRotation, targetRenderRotation) - rotationChangeThreshold);
                    else
                        _renderRotation = Quaternion.Slerp(
                            _renderRotation,
                            targetRenderRotation,
                            1f - Mathf.Exp(-rotationInterpolationSpeed * Time.deltaTime));
                else
                    _renderRotation = targetRenderRotation;
            }
            else
            {
                _renderPosition = targetRenderPosition;
                _renderRotation = targetRenderRotation;
            }

            if (enablePosition || enableRotation)
            {
                if (CanInterpolateWithinParent())
                {
                    _wasInterpolating = true;
                    if (enablePosition) renderNode.position = _renderPosition;
                    if (enableRotation) renderNode.rotation = _renderRotation;   
                }
                else if (_wasInterpolating)
                {
                    Teleport();
                    _wasInterpolating = false;
                }
            }
        }

        private bool CanInterpolateWithinParent()
        {
            if (allowInterpolationWithinBodyParent)
                return true;
            var parent = body.transform.parent;
            return !parent;
        }

        private void FixedUpdate()
        {
            _prevFixedPosition = _fixedPosition;
            _prevFixedRotation = _fixedRotation;
            _fixedPosition = body.position;
            _fixedRotation = body.rotation;
        }

        private void Teleport()
        {
            _fixedPosition = body.position;
            _fixedRotation = body.rotation;
            _prevFixedPosition = _fixedPosition;
            _prevFixedRotation = _fixedRotation;
            _renderPosition = _fixedPosition;
            _renderRotation = _fixedRotation;
            if (enablePosition) renderNode.position = _fixedPosition;
            if (enableRotation) renderNode.rotation = _fixedRotation;
        }

        private void OnFloatingOriginShifted() => Teleport();
    }
}