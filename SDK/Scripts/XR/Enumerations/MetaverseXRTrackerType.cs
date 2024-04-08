namespace MetaverseCloudEngine.Unity.XR
{
    public enum MetaverseXRTrackerType
    {
        /// <summary>
        /// No tracker type.
        /// </summary>
        None,
        /// <summary>
        /// The head tracker. Note that this is the base of the neck, not exactly the head.
        /// </summary>
        Head,
        /// <summary>
        /// The left elbow tracker.
        /// </summary>
        LeftElbow,
        /// <summary>
        /// A helper for IK solutions. This is the goal for the left elbow. This may not be available for all trackers.
        /// </summary>
        LeftElbowGoal,
        /// <summary>
        /// The right elbow tracker.
        /// </summary>
        RightElbow,
        /// <summary>
        /// A helper for IK solutions. This is the goal for the left elbow. This may not be available for all trackers.
        /// </summary>
        RightElbowGoal,
        /// <summary>
        /// The left hand tracker.
        /// </summary>
        LeftWrist,
        /// <summary>
        /// The right hand tracker.
        /// </summary>
        RightWrist,
        /// <summary>
        /// The left knee tracker.
        /// </summary>
        LeftKnee,
        /// <summary>
        /// A helper for IK solutions. This is the goal for the left knee. This may not be available for all trackers.
        /// </summary>
        LeftKneeGoal,
        /// <summary>
        /// The right knee tracker.
        /// </summary>
        RightKnee,
        /// <summary>
        /// A helper for IK solutions. This is the goal for the right knee. This may not be available for all trackers.
        /// </summary>
        RightKneeGoal,
        /// <summary>
        /// The left ankle tracker.
        /// </summary>
        LeftAnkle,
        /// <summary>
        /// The right ankle tracker.
        /// </summary>
        RightAnkle,
        /// <summary>
        /// The pelvis tracker.
        /// </summary>
        Pelvis,
    }
}