using System;
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
        public Mesh GetMesh()
        {
            if (string.IsNullOrEmpty(Value)) return null;
            var parts = Value.Split(';');
            if (parts.Length < 4) return null;

            if (!int.TryParse(parts[0], out var vertexCount)) return null;
            var vertices = new Vector3[vertexCount];
            var vertexParts = parts[1].Split(';');
            if (vertexParts.Length != vertexCount) return null;
            for (var i = 0; i < vertexCount; i++)
            {
                var coords = vertexParts[i].Split(',');
                if (coords.Length != 3) return null;
                if (!float.TryParse(coords[0], out var x)) return null;
                if (!float.TryParse(coords[1], out var y)) return null;
                if (!float.TryParse(coords[2], out var z)) return null;
                vertices[i] = new Vector3(x, y, z);
            }

            if (!int.TryParse(parts[2], out var triangleCount)) return null;
            var triangles = new int[triangleCount];
            var triangleParts = parts[3].Split(';');
            if (triangleParts.Length != triangleCount) return null;
            for (var i = 0; i < triangleCount; i++)
            {
                if (!int.TryParse(triangleParts[i], out var t)) return null;
                triangles[i] = t;
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
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