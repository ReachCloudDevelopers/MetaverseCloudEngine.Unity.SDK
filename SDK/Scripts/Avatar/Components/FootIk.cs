using System;
using Cysharp.Threading.Tasks.Triggers;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [RequireComponent(typeof(Animator))]
    [HideMonoScript]
    public class FootIk : TriInspectorMonoBehaviour
    {
        [Header("Main")]
        [Range(0, 1)] public float weight = 1f;
        public Transform rootTransform;

        [Header("Settings")]
        public float maxStep = 0.5f;
        public float footRadius = 0.15f;
        public LayerMask ground = Physics.DefaultRaycastLayers;
        public float offset;

        [Header("Speed")]
        public float hipsPositionSpeed = 1f;
        public float feetPositionSpeed = 2f;
        public float feetRotationSpeed = 90;
        public float weightFadeOutSpeed = 10f;
        public float weightFadeInSpeed = 1f;

        [Header("Weight")]
        [Range(0, 1)] public float hipsWeight = 0.75f;
        [Range(0, 1)] public float footPositionWeight = 1f;
        [Range(0, 1)] public float footRotationWeight = 1f;

        [FormerlySerializedAs("ShowDebug")]
        public bool showDebug = true;

        // Private variables
        private Vector3 _lIkPosition, _rIkPosition, _lNormal, _rNormal;
        private Quaternion _likRotation, _rikRotation, _lastLeftRotation, _lastRightRotation;
        private float _lastRFootHeight, _lastLFootHeight;
        private Animator _anim;
        private float _velocity;
        private float _falloffWeight;
        private float _lastHeight;
        private Vector3 _lastPosition;
        private bool 
            _lGrounded, 
            _rGrounded, 
            _isGrounded;
        private bool _didInitialGround;

        public bool IkActive { get; set; } = true;
        public Transform AnimatorTransform => rootTransform ? rootTransform : _anim.transform;

        // Initialization
        private void Start()
        {
            _anim = GetComponent<Animator>();
            if (!rootTransform)
                rootTransform = transform;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug) return;
            if (!_anim) return;
            DrawFootGizmo(HumanBodyBones.LeftFoot);
            DrawFootGizmo(HumanBodyBones.RightFoot);
        }

        // Updating the position of each foot.
        private void FixedUpdate()
        {
            Solve();
        }

        private void Solve()
        {
            if (weight == 0 || !IkActive || !_anim)
                return;

            if (!_anim.isHuman)
            {
                enabled = false;
                return;
            }

            var newPosition = LocalSpacePos();
            var speed = (_lastPosition - newPosition) / Time.deltaTime;
            _velocity = Mathf.Clamp(speed.magnitude, 1, speed.magnitude);
            _lastPosition = newPosition;

            // Raycast to the ground to find positions
            FeetSolver(HumanBodyBones.LeftFoot, ref _lIkPosition, ref _lNormal, ref _likRotation, ref _lGrounded); // Left foot
            FeetSolver(HumanBodyBones.RightFoot, ref _rIkPosition, ref _rNormal, ref _rikRotation, ref _rGrounded); // Right foot

            // Grounding
            GetGrounded();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (weight == 0 || !IkActive || !_anim || !_didInitialGround)
                return;

            // Pelvis height
            MovePelvisHeight();
            // Left foot IK
            MoveIK(AvatarIKGoal.LeftFoot, _lIkPosition, _lNormal, _likRotation, ref _lastLFootHeight, ref _lastLeftRotation);
            // Right foot IK
            MoveIK(AvatarIKGoal.RightFoot, _rIkPosition, _rNormal, _rikRotation, ref _lastRFootHeight, ref _lastRightRotation);
        }

        // Set the pelvis height.
        private void MovePelvisHeight()
        {
            // Get height
            var localSpacePos = LocalSpacePos();
            var leftOffset = _lIkPosition.y - localSpacePos.y;
            var rightOffset = _rIkPosition.y - localSpacePos.y;
            var totalOffset = (leftOffset < rightOffset) ? leftOffset : rightOffset;

            // Get hips position
            var newPosition = LocalSpacePos(_anim.bodyPosition);
            var newHeight = totalOffset * (hipsWeight * _falloffWeight);
            _lastHeight = Mathf.MoveTowards(_lastHeight, newHeight, hipsPositionSpeed * Time.deltaTime);
            newPosition.y += _lastHeight + offset;

            // Set position
            _anim.bodyPosition = WorldSpacePos(newPosition);
        }

        // Feet.
        private void MoveIK(
            AvatarIKGoal foot,
            Vector3 ikPosition,
            Vector3 normal,
            Quaternion ikRotation,
            ref float lastHeight,
            ref Quaternion lastRotation)
        {
            var position = _anim.GetIKPosition(foot);
            var rotation = _anim.GetIKRotation(foot);

            //Position
            ikPosition = WorldSpacePos(ikPosition);
            position = _anim.transform.InverseTransformPoint(position);
            ikPosition = _anim.transform.InverseTransformPoint(ikPosition);
            lastHeight = Mathf.MoveTowards(lastHeight, ikPosition.y, feetPositionSpeed * Time.deltaTime);
            position.y += lastHeight;

            position = _anim.transform.TransformPoint(position);
            position += WorldSpaceDir(normal) * offset;

            // Rotation
            var relative = Quaternion.Inverse(ikRotation * rotation) * rotation;
            lastRotation = Quaternion.RotateTowards(lastRotation, Quaternion.Inverse(relative), feetRotationSpeed * Time.deltaTime);
            rotation *= lastRotation;

            // Set IK
            _anim.SetIKPosition(foot, position);
            _anim.SetIKPositionWeight(foot, footPositionWeight * _falloffWeight);
            _anim.SetIKRotation(foot, rotation);
            _anim.SetIKRotationWeight(foot, footRotationWeight * _falloffWeight);
        }

        private void GetGrounded()
        {
            // Set Weight
            _isGrounded = _lGrounded || _rGrounded;
            if (_isGrounded)
                _didInitialGround = true;

            // Fading out MainWeight when is not grounded
            _falloffWeight = LerpValue(_falloffWeight, _isGrounded ? 1f : 0f, weightFadeInSpeed, weightFadeOutSpeed, Time.deltaTime) * weight;
        }

        public float LerpValue(float current, float desired, float increaseSpeed, float decreaseSpeed, float deltaTime)
        {
            if (Math.Abs(current - desired) < 0.0001f) return desired;
            if (current < desired) return Mathf.MoveTowards(current, desired, (increaseSpeed * _velocity) * deltaTime);
            else return Mathf.MoveTowards(current, desired, (decreaseSpeed * _velocity) * deltaTime);
        }

        private void DrawFootGizmo(HumanBodyBones bone)
        {
            var boneT = _anim.GetBoneTransform(bone);
            if (!boneT) return;
            var ray = new Ray(WorldSpacePos(LocalSpacePos(boneT.position)), WorldSpaceDir(Vector3.down));
            Gizmos.DrawWireSphere(ray.origin, footRadius);
            Gizmos.DrawWireSphere(ray.GetPoint(maxStep * 2), footRadius);
        }

        // Feet solver
        private void FeetSolver(HumanBodyBones foot, ref Vector3 ikPosition, ref Vector3 normal, ref Quaternion ikRotation, ref bool grounded)
        {
            if (!_anim)
                return;

            var footBone = _anim.GetBoneTransform(foot);
            if (!footBone)
            {
                grounded = false;
                return;
            }

            var localSpacePos = LocalSpacePos();
            var position = LocalSpacePos(footBone.position);
            position.y = localSpacePos.y + maxStep;

            // Add offset
            position -= normal * offset;
            var feetHeight = maxStep;

            if (Physics.SphereCast(WorldSpacePos(position), footRadius, WorldSpaceDir(Vector3.down), out var hit, maxStep * 2, ground, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(rootTransform))
                {
                    // Position (height)
                    ikPosition = LocalSpacePos(hit.point);
                    feetHeight = localSpacePos.y - ikPosition.y;

                    // Normal (Slope)
                    normal = LocalSpaceDir(hit.normal);

                    // Rotation (normal)
                    var localUp = WorldSpaceDir(Vector3.up);
                    var axis = Vector3.Cross(localUp, hit.normal);
                    var angle = Vector3.Angle(localUp, hit.normal);
                    ikRotation = Quaternion.AngleAxis(angle, axis);
                }
            }

            grounded = feetHeight < maxStep;

            if (!grounded)
            {
                ikPosition.y = localSpacePos.y - maxStep;
                ikRotation = Quaternion.identity;
            }
        }

        private Vector3 LocalSpacePos(Vector3 v)
        {
            return rootTransform.parent ? rootTransform.parent.InverseTransformPoint(v) : v;
        }

        private Vector3 LocalSpaceDir(Vector3 d)
        {
            return rootTransform.parent ? rootTransform.parent.InverseTransformDirection(d) : d;
        }

        private Vector3 LocalSpacePos()
        {
            return AnimatorTransform.localPosition;
        }

        private Vector3 WorldSpacePos(Vector3 v)
        {
            return rootTransform.parent ? rootTransform.parent.TransformPoint(v) : v;
        }

        private Vector3 WorldSpaceDir(Vector3 d)
        {
            return rootTransform.parent ? rootTransform.parent.TransformDirection(d) : d;
        }

    }
}