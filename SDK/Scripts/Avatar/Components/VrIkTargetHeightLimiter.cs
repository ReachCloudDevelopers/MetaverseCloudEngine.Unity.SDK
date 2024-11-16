using System;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    /// <summary>
    /// This component ensures that users don't "float" above the ground or awkwardly crouch super low if their HMD is
    /// too high above their character.
    /// </summary>
    [DefaultExecutionOrder(int.MaxValue)]
    [HideMonoScript]
    public class VrIkTargetHeightLimiter : TriInspectorMonoBehaviour
    {
        [SerializeField] private Transform headIkTarget;
        [SerializeField] private Transform leftHandIkTarget;
        [SerializeField] private Transform rightHandIkTarget;
        [SerializeField] private Transform trackingContainer;
        [LabelText("Max Height")]
        [SerializeField] private float heightValue = 1.6f;
        [LabelText("Min Height")]
        [SerializeField] private float minHeightValue = 0.5f;

        private bool _isHeightBeingLimited;

        private void OnValidate()
        {
            if (heightValue < minHeightValue)
                heightValue = minHeightValue;
        }

        private void LateUpdate()
        {
            CapHeight();
        }

        private void CapHeight()
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
            else if (headPos.y < minHeightValue)
            {
                _isHeightBeingLimited = true;
                
                var headExtraHeight = minHeightValue - headPos.y;
                headPos.y += headExtraHeight;
                headIkTarget.position = trackingContainer.TransformPoint(headPos);
                
                var lHandLocalPos = trackingContainer.InverseTransformPoint(leftHandIkTarget.parent.position);
                lHandLocalPos.y += headExtraHeight;
                leftHandIkTarget.position = trackingContainer.TransformPoint(lHandLocalPos);

                var rHandLocalPos = trackingContainer.InverseTransformPoint(rightHandIkTarget.parent.position);
                rHandLocalPos.y += headExtraHeight;
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