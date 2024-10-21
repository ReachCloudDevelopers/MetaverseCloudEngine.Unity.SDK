using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// A component that provides functionality necessary for a moving platform.
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Moving Platform")]
    [DefaultExecutionOrder(-int.MaxValue + 1)]
    public class MovingPlatform : TriInspectorMonoBehaviour
    {
        private const float BodyTransferCooldown = 0.3f;
        
        [SerializeField] private LayerMask ignoreLayers;
        [SerializeField] private bool localizePhysics = true;
        
        private Rigidbody _rigidbody;
        private Transform _transform;

        private bool _started;
        private readonly List<Rigidbody> _locallyRegisteredBodies = new();
        private readonly List<Rigidbody> _bodiesAwaitingUnregister = new();
        private readonly Dictionary<Rigidbody, OriginalRigidbodyState> _rbOriginalStateMap = new();
        
        private static readonly List<Rigidbody> GlobalCooldownBodies = new();
        private static readonly List<Rigidbody> GlobalRegisteredBodies = new();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _transform = transform;
        }

        private void Start()
        {
            _started = true;
        }

        private void Reset()
        {
            ignoreLayers = LayerMask.GetMask("Ignore Raycast", "TransparentFX", "Hit Box");
            if (!GetComponent<Rigidbody>())
                gameObject.AddComponent<Rigidbody>().isKinematic = true;
            if (!GetComponent<NetworkTransform>())
                gameObject.AddComponent<NetworkTransform>();
        }

        private void OnDisable()
        {
            if (MetaverseProgram.IsQuitting)
                return;

            foreach (var body in _locallyRegisteredBodies.ToArray())
            {
                if (!body) continue;
                if (HasReferenceBeenTaken(body)) continue;
                GlobalRegisteredBodies.Remove(body);
                ResetBodyState(body);
            }

            _locallyRegisteredBodies.Clear();
            _rbOriginalStateMap.Clear();
        }

        private void FixedUpdate()
        {
            ApplyLocalizedPhysics();
        }

        private void ApplyLocalizedPhysics()
        {
            if (!localizePhysics)
                return;

            var rbPosition = _rigidbody.position;
            var rbRotation = _rigidbody.rotation;

            var gravity = rbRotation * Physics.gravity;
            var cleanupList = false;

            var relativeVelocity = Quaternion.Inverse(rbRotation) * _rigidbody.GetLinearVelocity() * Time.deltaTime;
            var angularVelocity = Quaternion.Euler(Mathf.Rad2Deg * Time.deltaTime * _rigidbody.angularVelocity);
            var relativeAngularVelocity = Quaternion.Inverse(rbRotation) * (angularVelocity * rbRotation);
            var projectedRotation = rbRotation * relativeAngularVelocity;

            for (var i = _locallyRegisteredBodies.Count - 1; i >= 0; i--)
            {
                var body = _locallyRegisteredBodies[i];
                if (!body || body.transform.parent != _transform)
                {
                    _locallyRegisteredBodies.RemoveAt(i);
                    cleanupList = true;
                    continue;
                }
                
                if (HasReferenceBeenTaken(body))
                    continue;
                
                if (!_rbOriginalStateMap.TryGetValue(body, out var originalState))
                    continue;

                var localPosition = Quaternion.Inverse(rbRotation) * (body.position - rbPosition);
                localPosition += relativeVelocity;
                body.rotation = angularVelocity * body.rotation;
                body.position = rbPosition + (projectedRotation * localPosition);

                if (originalState.UseGravity)
                    body.AddForce(gravity, ForceMode.Acceleration);
            }

            if (cleanupList)
            {
                GlobalRegisteredBodies.RemoveAll(x => !x);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_started)
                return;

            if (ignoreLayers == (ignoreLayers | (1 << other.gameObject.layer)))
                return;

            var attachedRb = other.attachedRigidbody;
            if (attachedRb == null)
                return;

            if (GlobalRegisteredBodies.Contains(attachedRb))
                return;
            if (GlobalCooldownBodies.Contains(attachedRb))
                return;

            var go = attachedRb.gameObject;
            if (ignoreLayers == (ignoreLayers | (1 << go.layer)))
                return;
            if (other.gameObject != attachedRb.gameObject && 
                attachedRb.TryGetComponent(out Collider rootCollider) && rootCollider != other)
                // We want to make sure not to allow smaller, less significant
                // child colliders to un-register the body.
                return;

            MovingPlatform otherPlatform = null;
            if (attachedRb.transform.parent)
            {
                var parentRb = attachedRb.transform.parent.GetComponentInParent<Rigidbody>(); // REALLY SLOW!!!
                if (parentRb && (!parentRb.TryGetComponent(out otherPlatform) || otherPlatform == this))
                    // We don't want to un-parent an object
                    // that's already parented to another rigidbody.
                    // For example: A system of joints, or a ragdoll.
                    return;
            }
            
            var networkObject = attachedRb.GetComponentInParent<NetworkObject>();
            if (networkObject)
            {
                // We don't want to modify the parent state of a network object
                // that we don't have authority over.
                if (!networkObject.IsStateAuthority)
                    return;

                // We also don't want to modify the parent state of an object
                // that does not have the parent sync option enabled, this
                // we cause dramatic de-syncs.
                if (networkObject.TryGetComponent(out NetworkTransform tr) && !tr.synchronizationOptions.HasFlag(NetworkTransform.SyncOptions.Parent))
                    return;
            }

            RegisterBody(attachedRb, otherPlatform);
        }

        private void OnTriggerExit(Collider other)
        {
            var attachedRb = other.attachedRigidbody;
            if (attachedRb == null) return;
            var go = attachedRb.gameObject;
            if (ignoreLayers == (ignoreLayers | (1 << go.layer)))
                return;
            if (other.gameObject != attachedRb.gameObject && 
                attachedRb.TryGetComponent(out Collider rootCollider) && rootCollider != other)
                // We want to make sure not to allow smaller, less significant
                // child colliders to un-register the body.
                return;

            UnregisterBody(attachedRb);
        }

        private void RegisterBody(Rigidbody attachedRb, MovingPlatform otherPlatform = null)
        {
            if (_bodiesAwaitingUnregister.Remove(attachedRb))
            {
                GlobalRegisteredBodies.Add(attachedRb);
                return;
            }
            
            if (_locallyRegisteredBodies.Contains(attachedRb))
                return;
            
            _locallyRegisteredBodies.Add(attachedRb);
            GlobalRegisteredBodies.Add(attachedRb);
            CooldownBody(attachedRb);
            if (otherPlatform)
                otherPlatform.ResetBodyState(attachedRb);
            SetBodyState(attachedRb);
        }

        private static void CooldownBody(Rigidbody attachedRb)
        {
            // This ensures that this rigidbody can't continuously
            // be registered and unregistered immediately. This
            // prevents a jittering / vibrating effect.
            
            GlobalCooldownBodies.Add(attachedRb);
            MetaverseDispatcher.WaitForSeconds(BodyTransferCooldown, () =>
            {
                if (!attachedRb)
                {
                    GlobalCooldownBodies.RemoveAll(x => !x);
                    return;
                }

                GlobalCooldownBodies.Remove(attachedRb);
            });
        }

        private void SetBodyState(Rigidbody rb)
        {
            var attachedRbT = rb.transform;
            var originalState = new OriginalRigidbodyState
            {
                Parent = attachedRbT.parent,
                UseGravity = rb.useGravity,
            };

            _rbOriginalStateMap[rb] = originalState;

            var originalPos = attachedRbT.position;
            var originalRot = attachedRbT.rotation;
            attachedRbT.SetParent(_transform);
            attachedRbT.SetPositionAndRotation(originalPos, originalRot);
            if (localizePhysics)
                rb.useGravity = false;
        }

        private void UnregisterBody(Rigidbody rb)
        {
            if (!HasLooseReference(rb))
            {
                _bodiesAwaitingUnregister.Add(rb);
                MetaverseDispatcher.WaitForSeconds(BodyTransferCooldown, () => FinishUnregisteringBody(rb));
            }
            GlobalRegisteredBodies.Remove(rb);
        }
        
        private void FinishUnregisteringBody(Rigidbody rb)
        {
            if (!rb)
            {
                if (this) _bodiesAwaitingUnregister.RemoveAll(x => !x);
                return;
            }
            
            if (!_bodiesAwaitingUnregister.Contains(rb))
                return;

            try
            {
                if (HasReferenceBeenTaken(rb))
                    return;
                if (this && (HasLooseReference(rb) || HasStrongReference(rb)))
                    ResetBodyState(rb);
            }
            finally
            {
                _bodiesAwaitingUnregister?.Remove(rb);
                _locallyRegisteredBodies?.Remove(rb);
            }
        }

        private bool HasLooseReference(Rigidbody rb)
        {
            return _bodiesAwaitingUnregister.Contains(rb) && !GlobalRegisteredBodies.Contains(rb);
        }

        private bool HasStrongReference(Rigidbody rb)
        {
            return !_bodiesAwaitingUnregister.Contains(rb) && _locallyRegisteredBodies.Contains(rb) && GlobalRegisteredBodies.Contains(rb);
        }

        private bool HasReferenceBeenTaken(Rigidbody rb)
        {
            return _bodiesAwaitingUnregister.Contains(rb) && GlobalRegisteredBodies.Contains(rb);
        }

        private void ResetBodyState(Rigidbody rb)
        {
            if (_rbOriginalStateMap is null)
                return;
            
            if (!_rbOriginalStateMap.ContainsKey(rb))
                return;

            var originalState = _rbOriginalStateMap[rb];
            var attachedT = rb.transform;

            var currentPos = attachedT.position;
            var currentRot = attachedT.rotation;
            attachedT.SetParent(originalState.Parent);
            attachedT.SetPositionAndRotation(currentPos, currentRot);

            rb.useGravity = originalState.UseGravity;

            _rbOriginalStateMap.Remove(rb);
        }

        private class OriginalRigidbodyState
        {
            public bool UseGravity;
            public Transform Parent;
        }
    }
}
