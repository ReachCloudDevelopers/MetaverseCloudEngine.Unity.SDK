using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Ground Surface/Vehicles - Ground Surface Master", 0)]
    public class GroundSurfaceMaster : MonoBehaviour
    {
        public GroundSurface[] surfaceTypes;

        private static GroundSurfaceMaster _instance;
        public static GroundSurfaceMaster Instance
        {
            get
            {
                if (_instance == null)
                    _instance = MVUtils.FindObjectsOfTypeNonPrefabPooled<GroundSurfaceMaster>(true).FirstOrDefault() ?? Default();
                return _instance;
            }
        }
        
        private static GroundSurfaceMaster Default()
        {
            GroundSurfaceMaster gsm = new GameObject(nameof(GroundSurfaceMaster)).AddComponent<GroundSurfaceMaster>();
            gsm.surfaceTypes = new[]
            {
                new GroundSurface
                {
                    name = "Default",
                    friction = 1,
                    useColliderFriction = true,
                }
            };
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RegisterCreatedObjectUndo(gsm.gameObject, "Create Ground Surface Master");
#endif
            
            return gsm;
        }

        private void Awake()
        {
            if (Instance != this)
                Destroy(this);
        }
    }

    // Class for individual surface types
    [System.Serializable]
    public class GroundSurface
    {
        public string name = "Surface";
        public bool useColliderFriction;
        public float friction;
        [Tooltip("Always leave tire marks")]
        public bool alwaysScrape;
        [Tooltip("Rims leave sparks on this surface")]
        public bool leaveSparks;
        public AudioClip tireSnd;
        public AudioClip rimSnd;
        public AudioClip tireRimSnd;
    }
}