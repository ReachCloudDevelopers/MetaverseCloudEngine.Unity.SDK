using System;
using System.Globalization;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    public class SilverTauRPUMeshData : MonoBehaviour, ILandPlotObjectPropertyOfType<string>
    {
        public bool Hidden => true;
        public string Key => "rpu_mesh_data";
        public string DisplayName => "RPU Mesh Data";
        public string Description => "Serialized mesh data from RoomPlanUnityKit.";
        public object RawValue => Value;
        public string Value { get; set; }
        public event Action<object> ValueChanged;
        public event Action<string> ValidationFailed;
        
        public void Set(object obj)
        {
            if (obj is string str && str != Value)
            {
                Value = str;
                ValueChanged?.Invoke(Value);
                
                var m = GetMesh(str);
                if (m == null) return;
                var meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null) return;
                meshFilter.mesh = m;
                var meshCollider = GetComponent<MeshCollider>();
                if (meshCollider == null) return;
                meshCollider.sharedMesh = m;
            }
            
            if (obj is Mesh mesh)
            {
                var serialized = SerializeMeshData(mesh);
                if (serialized == Value) return;
                Value = serialized;
                ValueChanged?.Invoke(Value);
            }
        }
        
        /// <summary>
        /// Deserializes the stored string value into a Unity Mesh object.
        /// </summary>
        /// <returns>The deserialized Mesh, or null if deserialization fails.</returns>
        public static Mesh GetMesh(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var parts = value.Split(';');
            if (parts.Length < 2)
                return null;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vertexCount) || vertexCount <= 0)
                return null;

            var triCountIndex = 1 + vertexCount;
            if (triCountIndex >= parts.Length)
                return null;

            if (!int.TryParse(parts[triCountIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var triangleCount) || triangleCount <= 0)
                return null;

            var vertices = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                var vRaw = parts[1 + i];
                var coords = vRaw.Split(',');
                if (coords.Length != 3)
                    return null;

                if (!float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                    !float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                    !float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    return null;

                vertices[i] = new Vector3(x, y, z);
            }

            var triStart = triCountIndex + 1;
            var requiredLen = triStart + triangleCount;
            if (requiredLen > parts.Length)
                return null;

            var triangles = new int[triangleCount];
            for (int i = 0; i < triangleCount; i++)
            {
                if (!int.TryParse(parts[triStart + i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    return null;
                triangles[i] = idx;
            }

            var mesh = new Mesh
            {
                name = "RPU Mesh"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }


        private static string SerializeMeshData(Mesh mesh)
        {
            if (mesh is null) return string.Empty;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            return $"{vertices.Length};{string.Join(";", vertices.Select(v => $"{v.x},{v.y},{v.z}"))};" +
                   $"{triangles.Length};{string.Join(";", triangles.Select(t => t.ToString()))}";
        }
    }
}