using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Ground Surface/Vehicles - Ground Surface Instance", 1)]

    // Class for instances of surface types
    public class GroundSurfaceInstance : MonoBehaviour
    {
        [Tooltip("Which surface type to use from the GroundSurfaceMaster list of surface types")]
        public int surfaceType;
        [System.NonSerialized]
        public float friction;

        void Start() {
            // Set friction
            if (GroundSurfaceMaster.Instance.surfaceTypes[surfaceType].useColliderFriction) {
                PhysicMaterial sharedMat = GetComponent<Collider>().sharedMaterial;
                friction = sharedMat != null ? sharedMat.dynamicFriction * 2 : 1.0f;
            }
            else {
                friction = GroundSurfaceMaster.Instance.surfaceTypes[surfaceType].friction;
            }
        }
    }
}