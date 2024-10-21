using UnityEngine;
using UnityEngine.Events;

using System.Linq;
using System.Collections.Generic;

using MetaverseCloudEngine.Unity.Async;
using System;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Physix
{
    /// <summary>
    /// Generates a physics rig for a VR avatar.
    /// </summary>
    [DefaultExecutionOrder(int.MaxValue)] // Update after any custom IK systems.
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/VR Physics Rig")]
    [HideMonoScript]
    [Experimental]
    public class VRPhysicsRig : TriInspectorMonoBehaviour
    {
        private static readonly HumanBodyBones[] PhysicsBones = Enumerable
            .Range((int)HumanBodyBones.Spine, ((int)HumanBodyBones.RightHand - (int)HumanBodyBones.Spine) + 1)
            .Concat(new[] { (int)HumanBodyBones.Hips, (int)HumanBodyBones.UpperChest })
            .Where(x => x is not (int)HumanBodyBones.Head and not (int)HumanBodyBones.Neck)
            .Select(x => (HumanBodyBones)x)
            .ToArray();

        [SerializeField] private bool autoSetup = true;
        [SerializeField, Min(0)] private float generateDelay = 1f;
        [SerializeField] private float rigidBodyFreezeDelay = 1f;
        [DisableIf(nameof(IsActive))][SerializeField] private Rigidbody rootBody;
        [DisableIf(nameof(IsActive))][SerializeField] private Animator animationRig;
        [DisableIf(nameof(IsActive))][SerializeField] private int jointLayer = 8;
        [DisableIf(nameof(IsActive))][SerializeField] private float massScale = 10;
        [DisableIf(nameof(IsActive))][SerializeField] private float massScaleLimbRatio = 0.25f;
        [DisableIf(nameof(IsActive))][SerializeField] private float positionSpring = 5000;
        [DisableIf(nameof(IsActive))][SerializeField] private float positionDamper = 200;
        [DisableIf(nameof(IsActive))][SerializeField] private float strength = 500;
        [DisableIf(nameof(IsActive))][SerializeField] private PhysicsMaterial handMaterial;

        [Header("Events")]
        public UnityEvent onGenerated;
        public UnityEvent onDestroyed;

        private GameObject _container;
        private Transform[] _animatedBonesKeys;
        private Dictionary<Transform, Transform> _animatedBoneMap;
        private Dictionary<HumanBodyBones, Transform> _animatedPhysicsBones;
        private Dictionary<Transform, Transform> _physicsBoneMap;
        private Renderer[] _disabledRenderers;
        private bool _didStart;
        private bool _activating;
        private Action _inLateUpdate;
        private float _freezeRigidbodyTimeout;

        public bool IsActive => _container;
        public float Strength {
            get => strength;
            set => strength = value;
        }

        private void Start()
        {
            _didStart = true;
            if (autoSetup)
                Generate();
        }

        private void OnEnable()
        {
            if (autoSetup && _didStart)
                Generate();
        }

        private void LateUpdate()
        {
            if (_inLateUpdate != null)
            {
                _inLateUpdate();
                _inLateUpdate = null;
            }

            TrackNonPhysicsBones();
        }

        private void FixedUpdate()
        {
            if (MVUtils.CachedTime < _freezeRigidbodyTimeout && rootBody)
            {
                rootBody.SetLinearVelocity(Vector3.zero);
                rootBody.angularVelocity = Vector3.zero;

                var kinematic = rootBody.isKinematic;
                rootBody.isKinematic = true;
                rootBody.isKinematic = kinematic;

                rootBody.Sleep();
            }
        }

        private void TrackNonPhysicsBones()
        {
            if (_animatedBoneMap == null || _animatedBoneMap.Count == 0 || _animatedBonesKeys == null || _animatedBonesKeys.Length == 0)
                return;

            for (var i = 0; i < _animatedBonesKeys.Length; i++)
            {
                var boneToTrack = _animatedBonesKeys[i];
                if (!boneToTrack)
                    continue;

                var trackingBone = _animatedBoneMap[boneToTrack];
                if (!trackingBone)
                    continue;

                trackingBone.localPosition = boneToTrack.localPosition;
                trackingBone.localRotation = boneToTrack.localRotation;
                trackingBone.localScale = boneToTrack.localScale;
            }
        }

        private void OnDisable()
        {
            RemovePhysics();
        }

        /// <summary>
        /// Gets the physics bone specified.
        /// </summary>
        /// <param name="bone">The human bone to get.</param>
        /// <returns></returns>
        public Transform GetPhysicsBone(HumanBodyBones bone)
        {
            if (_animatedPhysicsBones != null && _animatedPhysicsBones.TryGetValue(bone, out var tr) && _physicsBoneMap.TryGetValue(tr, out var source))
                return source;
            return null;
        }

        /// <summary>
        /// Generates the physics rig. Note: There is a slight delay between generation time and the call of this function.
        /// </summary>
        [Button("Generate Physics")]
        public void Generate()
        {
            if (!Application.isPlaying)
                return;

            if (_activating)
                return;

            if (IsActive)
                return;

            _activating = true;

            MetaverseDispatcher.WaitForSeconds(generateDelay, () =>
            {
                _inLateUpdate = () =>
                {
                    if (!_activating)
                        return;

                    _activating = false;

                    if (!this)
                        return;

                    if (!isActiveAndEnabled)
                        return;

                    if (!animationRig || !animationRig.isHuman)
                        return;

                    var sourceHips = animationRig.GetBoneTransform(HumanBodyBones.Hips);
                    if (!sourceHips)
                        return;

                    _freezeRigidbodyTimeout = Time.time + rigidBodyFreezeDelay;

                    _container = new GameObject("Physics Rig Container");
                    _container.SetActive(false);
                    _container.transform.SetParent(transform);
                    _container.transform.localPosition = Vector3.zero;
                    _container.transform.localRotation = Quaternion.identity;

                    var instantiationSource = sourceHips.GetComponentInParent<Animator>().gameObject;
                    var rigClone = Instantiate(instantiationSource, _container.transform).transform;
                    rigClone.name = "Clone";
                    rigClone.localPosition = instantiationSource.transform.localPosition;
                    rigClone.localRotation = instantiationSource.transform.localRotation;

                    var rigAnimator = rigClone.GetComponent<Animator>();
                    if (rigAnimator)
                    {
                        rigAnimator.Play("TPose");
                        rigAnimator.Update(0);
                    }

                    var components = rigClone.GetComponentsInChildren<Component>(true).Where(x => x is not Renderer and not Transform and not Cloth);
                    foreach (var component in components)
                        if (component) Destroy(component);

                    _container.SetActive(true);

                    var containerBones = _container.GetComponentsInChildrenOrdered<Transform>();
                    var animatedBones = animationRig.GetBones(ignoredBones: PhysicsBones);
                    _animatedBonesKeys = animatedBones.Values.ToArray();
                    _animatedBoneMap = animatedBones.Values.Map(containerBones, (x, y) => x && y && x.name == y.name);

                    _animatedPhysicsBones = animationRig.GetBones(specificBones: PhysicsBones);
                    _physicsBoneMap = _animatedPhysicsBones.Values.Map(containerBones, (x, y) => x && y && x.name == y.name);

                    foreach (var (animatedPhysicsBone, physicsBone) in _physicsBoneMap)
                    {
                        physicsBone.gameObject.layer = jointLayer;

                        var rb = physicsBone.gameObject.AddComponent<Rigidbody>();
                        rb.mass = massScale / CalculateLimbMassRatio(rb.gameObject);

                        var joint = physicsBone.gameObject.AddComponent<ConfigurableJoint>();
                        joint.projectionMode = JointProjectionMode.PositionAndRotation;

                        if (physicsBone.parent)
                        {
                            var parentJoint = physicsBone.parent.GetComponentInParent<ConfigurableJoint>();
                            if (parentJoint)
                                joint.connectedBody = parentJoint.GetComponent<Rigidbody>();
                        }

                        var isHips = sourceHips.name == physicsBone.name;
                        var isSpine = isHips ||
                                      _animatedPhysicsBones[HumanBodyBones.Spine] == animatedPhysicsBone ||
                                      _animatedPhysicsBones[HumanBodyBones.Chest] == animatedPhysicsBone ||
                                      _animatedPhysicsBones[HumanBodyBones.UpperChest] == animatedPhysicsBone ||
                                      _animatedPhysicsBones[HumanBodyBones.LeftShoulder] == animatedPhysicsBone ||
                                      _animatedPhysicsBones[HumanBodyBones.RightShoulder] == animatedPhysicsBone;

                        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                        joint.angularXDrive = joint.angularYZDrive = new JointDrive
                        {
                            maximumForce = isSpine ? float.MaxValue : strength,
                            positionDamper = isSpine ? 10f : positionDamper,
                            positionSpring = isSpine ? float.MaxValue : positionSpring,
                        };

                        if (_animatedPhysicsBones[HumanBodyBones.LeftHand] == animatedPhysicsBone ||
                            _animatedPhysicsBones[HumanBodyBones.RightHand] == animatedPhysicsBone ||
                            _animatedPhysicsBones[HumanBodyBones.LeftLowerArm] == animatedPhysicsBone ||
                            _animatedPhysicsBones[HumanBodyBones.RightLowerArm] == animatedPhysicsBone)
                        {
                            const int handSolverIterations = 20;
                            const int handVelocitySolverIterations = 10;

                            rb.solverIterations = handSolverIterations;
                            rb.solverVelocityIterations = handVelocitySolverIterations;
                        }

                        if (joint.connectedBody && !isSpine)
                        {
                            var hitBox = new GameObject($"[HitBox] {physicsBone.name}")
                            {
                                layer = physicsBone.gameObject.layer
                            };

                            var hitBoxTransform = hitBox.transform;
                            var jointConnectedBodyTransform = joint.connectedBody.transform;
                            hitBoxTransform.SetParent(jointConnectedBodyTransform, false);

                            var col = hitBox.AddComponent<CapsuleCollider>();
                            var jointConnectedBodyPosition = jointConnectedBodyTransform.position;
                            var jointPosition = joint.transform.position;
                            var jointOffset = jointConnectedBodyPosition - jointPosition;
                            hitBoxTransform.up = jointOffset.normalized;
                            hitBoxTransform.position = (jointConnectedBodyPosition + jointPosition) / 2f;
                            col.height = jointOffset.magnitude;
                            col.radius = 0.075f; // TODO FIXME: Interpolate the radius based on the distance to the joint with weights.
                        }

                        var followTarget = physicsBone.GetOrAddComponent<ConfigurableJointFollowTarget>();
                        followTarget.Target = animatedPhysicsBone;

                        if (isHips)
                        {
                            followTarget.FollowAnchor = true;
                            followTarget.UseConnectedBodyLocalSpaceRotation = true;
                            joint.autoConfigureConnectedAnchor = false;
                            joint.connectedBody = rootBody;
                        }

                        rb.centerOfMass = Vector3.zero;
                        rb.inertiaTensor = Vector3.one;

                        physicsBone.localPosition = animatedPhysicsBone.localPosition;
                        rb.position = animatedPhysicsBone.position;
                        physicsBone.localRotation = animatedPhysicsBone.localRotation;
                        rb.rotation = animatedPhysicsBone.rotation;
                    }

                    _disabledRenderers = animationRig.GetComponentsInChildren<Renderer>();
                    foreach (var ren in _disabledRenderers)
                        ren.enabled = false;

                    var lHand = animationRig.GetBoneTransform(HumanBodyBones.LeftHand);
                    var lMiddleTip = animationRig.GetBoneTransform(HumanBodyBones.LeftMiddleDistal);
                    var lThumbTip = animationRig.GetBoneTransform(HumanBodyBones.LeftThumbDistal);

                    var rHand = animationRig.GetBoneTransform(HumanBodyBones.RightHand);
                    var rMiddleTip = animationRig.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
                    var rThumbTip = animationRig.GetBoneTransform(HumanBodyBones.RightThumbDistal);

                    if (lHand && lMiddleTip && lThumbTip && rHand && rMiddleTip && rThumbTip)
                    {
                        SetupPhysicsHand(lHand, lMiddleTip, lThumbTip);
                        SetupPhysicsHand(rHand, rMiddleTip, rThumbTip);
                    }

                    var rbs = _container.GetComponentsInChildren<Rigidbody>(true);
                    foreach (var rb in rbs)
                        rb.Sleep();

                    onGenerated?.Invoke();
                };
            });
        }

        /// <summary>
        /// Removes the physics rig that was generated.
        /// </summary>
        [Button("Remove Physics")]
        public void RemovePhysics()
        {
            if (!Application.isPlaying)
                return;

            _activating = false;

            var destroyed = false;
            if (_container)
            {
                destroyed = true;
                Destroy(_container);
            }
            _container = null;

            if (_disabledRenderers != null && _disabledRenderers.Length > 0)
            {
                foreach (var ren in _disabledRenderers)
                    if (ren) ren.enabled = true;
                _disabledRenderers = null;
            }

            _animatedBoneMap = null;
            _animatedBonesKeys = null;
            _physicsBoneMap = null;
            _animatedPhysicsBones = null;

            if (destroyed)
                onDestroyed?.Invoke();
        }

        private void SetupPhysicsHand(Transform wrist, Transform middleTip, Transform thumbTip)
        {
            if (!_physicsBoneMap.TryGetValue(wrist, out var physicsWrist))
                return;

            var middle = wrist.InverseTransformPoint(middleTip.transform.position);
            var thumb = wrist.InverseTransformPoint(thumbTip.position);
            var handMiddleOffset = middle / 2;
            var handUp = handMiddleOffset.normalized;
            var handRight = thumb.normalized;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.GetComponentsInChildren<Renderer>(true).ForEach(Destroy);
            cube.GetComponentsInChildren<MeshFilter>(true).ForEach(Destroy);

            cube.transform.SetParent(physicsWrist, false);
            cube.transform.localPosition = handMiddleOffset;
            cube.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(handUp, handRight), handUp);
            cube.transform.localScale = new Vector3(Mathf.Abs(thumb.x * 2), Mathf.Abs(middle.y), Mathf.Abs(thumb.x) * 0.75f);
            cube.layer = jointLayer;
            cube.GetComponent<Collider>().sharedMaterial = handMaterial;
        }

        private float CalculateLimbMassRatio(GameObject bone)
        {
            return Mathf.Pow(1 + massScaleLimbRatio, bone.GetComponentsInParent<ConfigurableJoint>(true).Count());
        }
    }
}