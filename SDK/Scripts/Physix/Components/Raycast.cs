using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Unity.Components;

using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// A helper component that exposes the <see cref="Physics"/> raycast module to the
    /// Unity front-end.
    /// </summary>
    [DeclareFoldoutGroup("Raycast")]
    [DeclareFoldoutGroup("Rigidbody")]
    [DeclareFoldoutGroup("Events")]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Raycast")]
    public class Raycast : TriInspectorMonoBehaviour
    {
        [Tooltip("(Optional) This is for Raycast Receiver's to filter this raycast.")]
        [Group("Raycast")][SerializeField] private string identifier;
        [Group("Raycast")][SerializeField] private LayerMask layerMask = Physics.DefaultRaycastLayers;
        [Group("Raycast")][SerializeField] private float maxDistance = Mathf.Infinity;
        [Group("Raycast")][SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal;

        [Group("Rigidbody")][SerializeField] private bool applyRigidbodyForce;
        [Group("Rigidbody")][SerializeField] private float forceToApply;

        [Group("Events")][SerializeField] private UnityEvent<GameObject> onHit;
        [Group("Events")][SerializeField] private UnityEvent<Vector3> onHitPoint;
        [Group("Events")][SerializeField] private UnityEvent<Vector3> onHitNormal;
        [Group("Events")][SerializeField] private UnityEvent<float> onHitDistance;
        [Group("Events")][SerializeField] private UnityEvent<Quaternion> onHitNormalRotation;
        [Group("Events")][SerializeField] private UnityEvent<RaycastHit> onRaycastHit;
        [Group("Events")][SerializeField] private UnityEvent onMiss;

        /// <summary>
        /// The Raycast's identifier.
        /// </summary>
        public string Identifier => identifier;

        public void PerformRaycast(bool forceFixedUpdate)
        {
            if (forceFixedUpdate && !Time.inFixedTimeStep)
            {
                UniTask.Void(async () =>
                {
                    await UniTask.WaitForFixedUpdate();
                    PerformRaycast();
                });
                return;
            }
            
            if (!CanPerform())
            {
                return;
            }

            if (GetRay(out Ray ray) && Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance, layerMask, triggerInteraction))
            {
                onRaycastHit?.Invoke(hitInfo);
                onHit?.Invoke(hitInfo.collider.gameObject);
                onHitPoint?.Invoke(hitInfo.point);
                onHitNormal?.Invoke(hitInfo.normal);
                onHitNormalRotation?.Invoke(Quaternion.LookRotation(hitInfo.normal));
                onHitDistance?.Invoke(hitInfo.distance);

                if (applyRigidbodyForce && hitInfo.rigidbody)
                {
                    hitInfo.rigidbody.AddForceAtPosition(transform.forward * forceToApply, hitInfo.point, ForceMode.Impulse);
                }

                if (hitInfo.collider.TryGetComponent(out RaycastReceiver receiver) || (hitInfo.rigidbody && hitInfo.rigidbody.TryGetComponent(out receiver)))
                {
                    receiver.NotifyHit(this, hitInfo);
                }
            }
            else
            {
                onMiss?.Invoke();
            }
        }

        /// <summary>
        /// Performs a raycast.
        /// </summary>
        public void PerformRaycast()
        {
            PerformRaycast(true);
        }

        public virtual bool CanPerform() => true;

        public virtual bool GetRay(out Ray ray)
        {
            ray = new(transform.position, transform.forward);
            return true;
        }
    }
}
