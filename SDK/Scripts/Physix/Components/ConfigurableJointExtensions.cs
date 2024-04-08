using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix
{
    public static class ConfigurableJointExtensions
    {
        public static Quaternion GetRotationRelativeToConnectedBody(this ConfigurableJoint joint, Quaternion worldRotation)
        {
            if (joint.connectedBody)
            {
                return Quaternion.Inverse(joint.connectedBody.transform.rotation) * worldRotation;
            }

            return worldRotation;
        }

        /// <summary>
        /// Sets a joint's targetRotation to match a given local rotation.
        /// The joint transform's local rotation must be cached on Start and passed into this method.
        /// </summary>
        public static void SetTargetRotationLocal(this ConfigurableJoint joint, Quaternion targetLocalRotation, Quaternion startLocalRotation)
        {
            if (joint.configuredInWorldSpace)
            {
                Debug.LogError("SetTargetRotationLocal should not be used with joints that are configured in world space. For world space joints, use SetTargetRotation.", joint);
                return;
            }
            SetTargetRotationInternal(joint, targetLocalRotation, startLocalRotation, Space.Self);
        }

        /// <summary>
        /// Sets a joint's targetRotation to match a given world rotation.
        /// The joint transform's world rotation must be cached on Start and passed into this method.
        /// </summary>
        public static void SetTargetRotation(this ConfigurableJoint joint, Quaternion targetWorldRotation, Quaternion startWorldRotation)
        {
            if (!joint.configuredInWorldSpace)
            {
                Debug.LogError("SetTargetRotation must be used with joints that are configured in world space. For local space joints, use SetTargetRotationLocal.", joint);
                return;
            }
            SetTargetRotationInternal(joint, targetWorldRotation, startWorldRotation, Space.World);
        }

        public static void SetTargetPositionLocal(this ConfigurableJoint joint, Vector3 targetLocalPosition)
        {
            if (joint.configuredInWorldSpace)
            {
                Debug.LogError("SetTargetPositionLocal should not be used with joints that are configured in world space. For world space joints, use SetTargetPosition", joint);
                return;
            }
            //SetTargetPositionInternal(joint, targetLocalPosition, Space.Self);
        }

        static void SetTargetPositionInternal(ConfigurableJoint joint, Vector3 targetPosition, Vector3 startPosition, Space space)
        {
            // Calculate the rotation expressed by the joint's axis and secondary axis
            Vector3 right = joint.axis;
            Vector3 forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Quaternion worldToJointSpace = Quaternion.LookRotation(forward, up);

            // Transform into world space
            Quaternion jointToWorldSpace = Quaternion.Inverse(worldToJointSpace);
            Vector3 resultPosition = Vector3.zero;

            if (space == Space.Self)
            {
                resultPosition = jointToWorldSpace * (targetPosition - startPosition);
            }
            else
            {
                //resultPosition = Quaternion.Inverse()
            }
        }

        static void SetTargetRotationInternal(ConfigurableJoint joint, Quaternion targetRotation, Quaternion startRotation, Space space)
        {
            Quaternion resultRotation = GetJointRotationInternal(joint, targetRotation, startRotation, space);
            joint.targetRotation = resultRotation;
        }

        private static Quaternion GetJointRotationInternal(ConfigurableJoint joint, Quaternion targetRotation, Quaternion startRotation, Space space)
        {
            // Calculate the rotation expressed by the joint's axis and secondary axis
            Vector3 right = joint.axis;
            Vector3 forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Quaternion worldToJointSpace = Quaternion.LookRotation(forward, up);

            // Transform into world space
            Quaternion jointToWorldSpace = Quaternion.Inverse(worldToJointSpace);

            // Counter-rotate and apply the new local rotation.
            // Joint space is the inverse of world space, so we need to invert our value
            if (space == Space.World)
                jointToWorldSpace *= startRotation * Quaternion.Inverse(targetRotation);
            else
                jointToWorldSpace *= Quaternion.Inverse(targetRotation) * startRotation;

            // Transform back into joint space
            jointToWorldSpace *= worldToJointSpace;
            return jointToWorldSpace;
        }
    }
}
