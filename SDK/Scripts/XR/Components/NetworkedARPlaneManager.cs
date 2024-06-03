using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MetaverseCloudEngine.Unity
{
    [RequireComponent(typeof(ARPlaneManager))]
    public class NetworkedARPlaneManager : NetworkObjectBehaviour
    {
        private ARPlaneManager _arPlaneManager;
        private readonly Dictionary<TrackableId, MeshRenderer> _planeMeshes = new();

        protected override void Awake()
        {
            base.Awake();
            
            _arPlaneManager = GetComponent<ARPlaneManager>();
        }

        protected override void RegisterNetworkRPCs()
        {
            base.RegisterNetworkRPCs();
            
            NetworkObject.RegisterRPC((short)NetworkRpcType.ARPlaneAdded, ARPlaneAddedRpc);
            NetworkObject.RegisterRPC((short)NetworkRpcType.ARPlaneRemoved, ARPlaneRemovedRpc);
            NetworkObject.RegisterRPC((short)NetworkRpcType.ARPlaneUpdated, ARPlaneUpdatedRpc);
        }

        protected override void UnRegisterNetworkRPCs()
        {
            base.UnRegisterNetworkRPCs();
            
            NetworkObject.UnregisterRPC((short)NetworkRpcType.ARPlaneAdded, ARPlaneAddedRpc);
            NetworkObject.UnregisterRPC((short)NetworkRpcType.ARPlaneRemoved, ARPlaneRemovedRpc);
            NetworkObject.UnregisterRPC((short)NetworkRpcType.ARPlaneUpdated, ARPlaneUpdatedRpc);
        }

        private void OnEnable()
        {
            _arPlaneManager.planesChanged += OnPlanesChanged;
        }

        private void OnDisable()
        {
            _arPlaneManager.planesChanged -= OnPlanesChanged;
        }

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            if (!NetworkObject.IsInputAuthority)
                return;
            
            foreach (var addedPlane in args.added)
            {
                NetworkObject.InvokeRPC((short)NetworkRpcType.ARPlaneAdded, NetworkMessageReceivers.Others, new object[]
                {
                    addedPlane.center,
                    addedPlane.extents,
                    (int)addedPlane.alignment,
                    (int)addedPlane.classification,
                    (long)addedPlane.trackableId.subId1,
                    (long)addedPlane.trackableId.subId2,
                    addedPlane.normal,
                    addedPlane.boundary.ToArray()
                    
                }, buffered: false);
            }

            foreach (var removedPlane in args.removed)
            {
                NetworkObject.InvokeRPC((short)NetworkRpcType.ARPlaneRemoved, NetworkMessageReceivers.Others, new object[]
                {
                    (long)removedPlane.trackableId.subId1,
                    (long)removedPlane.trackableId.subId2
                }, buffered: false);
            }
            
            foreach (var updatedPlane in args.updated)
            {
                NetworkObject.InvokeRPC((short)NetworkRpcType.ARPlaneUpdated, NetworkMessageReceivers.Others, new object[]
                {
                    updatedPlane.center,
                    updatedPlane.extents,
                    (int)updatedPlane.alignment,
                    (int)updatedPlane.classification,
                    (long)updatedPlane.trackableId.subId1,
                    (long)updatedPlane.trackableId.subId2,
                    updatedPlane.normal,
                    updatedPlane.boundary.ToArray()
                }, buffered: false);
            }
        }

        private void ARPlaneAddedRpc(short procedureId, int senderId, object content)
        {
            var args = (object[]) content;
            var center = (Vector3) args[0];
            var extents = (Vector2) args[1];
            var alignment = (PlaneAlignment) args[2];
            var classification = (PlaneClassification) args[3];
            var trackableId = new TrackableId((ulong)(long) args[4], (ulong)(long) args[5]);
            var normal = (Vector3) args[6];
            var boundary = (Vector2[]) args[7];

            var planeMesh = new GameObject("AR Plane").AddComponent<MeshRenderer>();
            planeMesh.transform.position = center;
            planeMesh.transform.rotation = Quaternion.LookRotation(normal);
            
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i < boundary.Length; i++)
            {
                var vertex = new Vector3(boundary[i].x, 0, boundary[i].y);
                vertices.Add(vertex);
                indices.Add(i);
            }
            
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices.ToArray(), MeshTopology.LineStrip, 0);
            
            planeMesh.gameObject.AddComponent<MeshFilter>().mesh = mesh;
            _planeMeshes.Add(trackableId, planeMesh);
        }

        private void ARPlaneRemovedRpc(short procedureId, int senderId, object content)
        {
            var args = (object[]) content;
            var trackableId = new TrackableId((ulong)(long) args[0], (ulong)(long) args[1]);
            
            if (!_planeMeshes.TryGetValue(trackableId, out var planeMesh))
                return;

            Destroy(planeMesh.GetComponent<MeshFilter>().mesh);
            Destroy(planeMesh.gameObject);
            _planeMeshes.Remove(trackableId);
        }

        private void ARPlaneUpdatedRpc(short procedureId, int senderId, object content)
        {
            var args = (object[]) content;
            var center = (Vector3) args[0];
            var extents = (Vector2) args[1];
            var alignment = (PlaneAlignment) args[2];
            var classification = (PlaneClassification) args[3];
            var trackableId = new TrackableId((ulong)(long) args[4], (ulong)(long) args[5]);
            var normal = (Vector3) args[6];
            var boundary = (Vector2[]) args[7];
            
            if (!_planeMeshes.TryGetValue(trackableId, out var planeMesh))
                return;

            planeMesh.transform.position = center;
            planeMesh.transform.rotation = Quaternion.LookRotation(normal);
            
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i < boundary.Length; i++)
            {
                var vertex = new Vector3(boundary[i].x, 0, boundary[i].y);
                vertices.Add(vertex);
                indices.Add(i);
            }
            
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices.ToArray(), MeshTopology.LineStrip, 0);
            
            planeMesh.gameObject.GetComponent<MeshFilter>().mesh = mesh;
        }
    }
}
