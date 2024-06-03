namespace MetaverseCloudEngine.Unity.Networking.Enumerations
{
    /// <summary>
    /// A simple enumeration of all the RPC types that are used in the network.
    /// </summary>
    public enum NetworkRpcType : short
    {
        /// <summary>
        /// A user has calibrated their VR IK.
        /// </summary>
        VrIkCalibrate,
        /// <summary>
        /// Request the calibration data for the VR IK.
        /// </summary>
        VrIkRequestCalibrationData,

        /// <summary>
        /// Used by the component <see cref="NetworkObjectRpc"/> when sending an RPC.
        /// </summary>
        NetworkObjectRpc,
        
        /// <summary>
        /// Sent by the XR hand poser when the hand pose has changed.
        /// </summary>
        XRHandPoseFlags,

        /// <summary>
        /// Sent by the network variables component when a variable has changed. 
        /// </summary>
        NetworkVariablesUpdate,
        /// <summary>
        /// Requests the network variables from the state authority.
        /// </summary>
        NetworkVariablesRequest,

        /// <summary>
        /// Sent whenever a PlayMaker FSM variable has changed.
        /// </summary>
        PlayMakerFsmNetworkVariablesUpdate,
        /// <summary>
        /// Requests the PlayMaker FSM variables from the state authority.
        /// </summary>
        PlayMakerFsmNetworkVariablesRequest,

        /// <summary>
        /// Sent whenever the VR IK calibration controller has calibrated.
        /// </summary>
        VrIkCalibrationControllerFunctionsCalibrate,
        /// <summary>
        /// Request the VR IK calibration controller to calibrate.
        /// </summary>
        VrIkCalibrationControllerFunctionsRequestCalibrate,

        /// <summary>
        /// Sent when the text of a TextMeshPro component has changed.
        /// </summary>
        TextMeshProTextUpdate,
        /// <summary>
        /// Requests the text of a TextMeshPro component.
        /// </summary>
        TextMeshProTextRequest,

        /// <summary>
        /// Sent when the state of a ragdoll has changed.
        /// </summary>
        RagdollStateChanged,
        /// <summary>
        /// Requests the state of a ragdoll.
        /// </summary>
        RagdollPositionUpdate,

        HitpointsRequestApplyDamage = 50,
        HitpointsDied,
        HitpointsValue,
        
        SetPosition = 60,
        SetRotation,
        SetScale,
        
        RequestLoadAvatar = 70,
        
        ARPlaneAdded = 80,
        ARPlaneUpdated,
        ARPlaneRemoved,
    }
}
