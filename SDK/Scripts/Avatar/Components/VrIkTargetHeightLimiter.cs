using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    /// <summary>
    /// This component ensures that users don't "float" above the ground if their HMD is
    /// too high above their character.
    /// </summary>
    [DefaultExecutionOrder(int.MaxValue)]
    public class VrIkTargetHeightLimiter : MonoBehaviour
    {
        [SerializeField] private Transform headIkTarget;
        [SerializeField] private Transform leftHandIkTarget;
        [SerializeField] private Transform rightHandIkTarget;
        [SerializeField] private Transform trackingContainer;
        [SerializeField] private float heightValue = 1.6f;

        private bool _isHeightBeingLimited;
        
        private void LateUpdate()
        {
            var headPos = trackingContainer.InverseTransformPoint(headIkTarget.parent.position);
            if (headPos.y > heightValue)
            {
                _isHeightBeingLimited = true;
                
                var headExtraHeight = headPos.y - heightValue;
                headPos.y -= headExtraHeight;
                headIkTarget.position = trackingContainer.TransformPoint(headPos);
                
                var lHandLocalPos = trackingContainer.InverseTransformPoint(leftHandIkTarget.parent.position);
                lHandLocalPos.y -= headExtraHeight;
                leftHandIkTarget.position = trackingContainer.TransformPoint(lHandLocalPos);

                var rHandLocalPos = trackingContainer.InverseTransformPoint(rightHandIkTarget.parent.position);
                rHandLocalPos.y -= headExtraHeight;
                rightHandIkTarget.position = trackingContainer.TransformPoint(rHandLocalPos);
            }
            else
            {
                if (!_isHeightBeingLimited)
                    return;
                _isHeightBeingLimited = false;
                headIkTarget.localPosition = Vector3.zero;
                leftHandIkTarget.localPosition = Vector3.zero;
                rightHandIkTarget.localPosition = Vector3.zero;
            }
        }
    }
}