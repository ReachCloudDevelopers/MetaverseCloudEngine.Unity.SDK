using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// A helper class to add a fake recentering offset. This is useful for things like sitting down on a chair in VR.
    /// </summary>
    [DefaultExecutionOrder(-int.MaxValue)]
    public class XRSimulatedRecenter : TriInspectorMonoBehaviour
    {
        [Tooltip("A delay to apply to the recenter. This could be useful if there are changes being made to the HMD before recenetering.")]
        [SerializeField] private float recenterDelay = 0.1f;
        [Tooltip("The root player object.")]
        [SerializeField] private Transform root;
        [Tooltip("The HMD device.")]
        [SerializeField] private Transform hmdTransform;
        [Tooltip("An additional offset to apply when performing the recenter.")]
        [SerializeField] private Vector3 targetRecenterPos = new Vector3(0, 1.7f, 0);

        private bool _recentering = false;

        private void OnDisable() => Clear();

        /// <summary>
        /// Adds an offset to this transform to recenter it's origin.
        /// </summary>
        public void Recenter()
        {
            if (_recentering)
                return;

            _recentering = true;

            MetaverseDispatcher.WaitForSeconds(recenterDelay, () =>
            {
                if (!this)
                    return;

                if (!_recentering)
                    return;

                _recentering = false;

                if (!isActiveAndEnabled)
                    return;

                if (!root || !hmdTransform)
                    return;

                Vector3 targetHMDWorldPos = transform.parent ? transform.parent.TransformPoint(targetRecenterPos) : targetRecenterPos;
                AlignParentFromChild(transform, hmdTransform, targetHMDWorldPos, root.transform.rotation);
            });
        }

        /// <summary>
        /// Clears any recentering offset.
        /// </summary>
        public void Clear()
        {
            _recentering = false;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        // Source: https://answers.unity.com/questions/460064/align-parent-object-using-child-object-as-point-of.html
        private static void AlignParentFromChild(Transform parent, Transform child, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (parent && child)
            {
                Quaternion rotationDelta = Quaternion.Inverse(parent.rotation) * Quaternion.LookRotation(Vector3.ProjectOnPlane(child.forward, parent.up).normalized, parent.up);
                parent.rotation = targetRotation * Quaternion.Inverse(rotationDelta);
                parent.position = targetPosition + (parent.position - child.position);
            }
        }
    }
}