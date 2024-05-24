using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO.RealSense
{
    /// <summary>
    /// This class is responsible for performing a world-space raycast
    /// against the intel realsense depth image.
    /// </summary>
    [RequireComponent(typeof(IntelRealSenseTextureSource))]
    public class IntelRealSenseWorldSpaceDepthRay : MonoBehaviour
    {
        [Tooltip("The transform from which the mesh ")]
        [SerializeField] private Transform projectionSource;

        [Tooltip("From this transform the direction that the ray will be cast.")]
        [SerializeField] private Vector3 rayDirection = Vector3.forward;
        [Tooltip("The distance of the ray.")]
        [SerializeField] private float rayDistance;
        [Tooltip("If self, the ray will be cast in the local space of the transform. If world, the ray will be cast in world space.")]
        [SerializeField] private Space rayDirectionSpace = Space.Self;

        private IntelRealSenseTextureSource _textureSource;

        public struct RayHitInformation
        {
            public bool Hit;
            public Vector3 HitPoint;
        }

        private void Awake()
        {
            _textureSource = GetComponent<IntelRealSenseTextureSource>();
        }

        private RayHitInformation PerformRayCast()
        {
            var hitInfo = new RayHitInformation();
            
            if (_textureSource == null)
            {
                Debug.LogError("IntelRealSenseTextureSource is null. Please make sure that the IntelRealSenseTextureSource component is attached to the same GameObject.");
                return hitInfo;
            }
            
            if (projectionSource == null)
            {
                Debug.LogError("Projection source is null. Please make sure that the projection source is assigned.");
                return hitInfo;
            } 

            var frame = _textureSource.DequeueNextFrame();
            if (frame == null)
                return hitInfo;

            

            return hitInfo;
        }
    }
}