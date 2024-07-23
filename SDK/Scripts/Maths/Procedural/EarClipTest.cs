using System.Linq;
using MetaverseCloudEngine.Unity.Maths.Procedural;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class EarClipTest : MonoBehaviour
    {
        private void Update()
        {
            var mesh = MeshingAPI.EarClipPoints(
                GetComponentsInChildren<Transform>()
                    .Where(x => x.transform != transform)
                    .Select(x => x.localPosition) 
                    .ToArray(),
                mesh: GetComponent<MeshFilter>().sharedMesh);
            
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }
}