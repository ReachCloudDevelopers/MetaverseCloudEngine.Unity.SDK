#if MV_META_CORE
using Meta.XR.BuildingBlocks;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [RequireComponent(typeof(RoomMeshEvent))]
    // ReSharper disable once InconsistentNaming
    public class OVRRoomMeshMaterial : TriInspectorMonoBehaviour
    {
        [SerializeField] private Material roomMeshMaterial;

        private RoomMeshEvent _roomMeshEvent;

        private void Awake()
        {
            _roomMeshEvent = GetComponent<RoomMeshEvent>();
            if (_roomMeshEvent == null)
            {
                MetaverseProgram.Logger.LogError("[OVRRoomMeshMaterial] Requires a RoomMeshEvent component.");
                return;
            }
            _roomMeshEvent.OnRoomMeshLoadCompleted.AddListener(OnRoomMeshUpdated);
        }

        private void OnDestroy()
        {
            if (_roomMeshEvent != null)
                _roomMeshEvent.OnRoomMeshLoadCompleted.RemoveListener(OnRoomMeshUpdated);
        }

        private void OnRoomMeshUpdated(MeshFilter roomMesh)
        {
            if (roomMesh == null || roomMesh.sharedMesh == null)
            {
                MetaverseProgram.Logger.LogWarning("[OVRRoomMeshMaterial] Room mesh is not available or has no shared mesh.");
                return;
            }

            if (roomMeshMaterial != null)
            {
                roomMesh.GetComponent<Renderer>().material = roomMeshMaterial;
            }
            else
            {
                MetaverseProgram.Logger.LogWarning("[OVRRoomMeshMaterial] Room mesh material is not assigned.");
            }
        }
    }
}
#endif