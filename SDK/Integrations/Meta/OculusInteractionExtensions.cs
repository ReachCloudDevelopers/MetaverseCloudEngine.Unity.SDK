#if MV_META_INTERACTION && METAVERSE_CLOUD_ENGINE

using UnityEngine;
using Oculus.Interaction.Body.Input;

namespace MetaverseCloudEngine.Unity.Meta
{
    public static class OculusInteractionExtensions
    {
        public static Pose GetBodyRootPoseOrIdentity(this IBody body)
        {
            return body.GetRootPose(out var pose) ? pose : Pose.identity;
        }

        public static Pose GetJointPoseOrIdentity(this IBody body, BodyJointId bodyJointId)
        {
            return body.GetJointPose(bodyJointId, out var pose) ? pose : Pose.identity;
        }

        public static Pose GetJointPoseLocalOrIdentity(this IBody body, BodyJointId bodyJointId)
        {
            return body.GetJointPoseLocal(bodyJointId, out var pose) ? pose : Pose.identity;
        }

        public static Pose GetJointPoseFromRootOrIdentity(this IBody body, BodyJointId bodyJointId)
        {
            return body.GetJointPoseFromRoot(bodyJointId, out var pose) ? pose : Pose.identity;
        }
    }
}

#endif