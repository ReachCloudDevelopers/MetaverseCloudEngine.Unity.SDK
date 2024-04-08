using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering
{
    /// <summary>
    /// An interface that is implemented to allow the <see cref="CameraDistanceManager"/> to
    /// measure the distance to an object. Use <see cref="CameraDistanceManager.AddMeasurer(IMeasureCameraDistance)"/> and
    /// <see cref="CameraDistanceManager.RemoveMeasurer(IMeasureCameraDistance)"/> to add/remove measurable objects.
    /// </summary>
    public interface IMeasureCameraDistance
    {
        /// <summary>
        /// The source position to use for measurements between the camera
        /// and this object.
        /// </summary>
        Vector3 CameraMeasurementPosition { get; }

        /// <summary>
        /// A callback that is invoked by the <see cref="CameraDistanceManager"/> when it
        /// has successfully measured the distance between this object and the <paramref name="cam"/>.
        /// </summary>
        /// <param name="cam">The camera that's being used for the measurement.</param>
        /// <param name="sqrDistance">The distance to the camera object from the <see cref="CameraMeasurementPosition"/>.</param>
        void OnCameraDistance(Camera cam, float sqrDistance);
    }
}