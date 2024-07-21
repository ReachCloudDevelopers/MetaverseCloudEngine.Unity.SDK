using System.Linq;
using MetaverseCloudEngine.Unity.Maths.Procedural;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    [ExecuteInEditMode]
    public class MeshingTest : MonoBehaviour
    {
        public float alpha = 1f;
        public float planeDistanceTolerance = 1f;
        
        // Update is called once per frame
        private void Update()
        {
            var mesh = MeshingAPI.GenerateConcaveMesh(
                GetComponentsInChildren<Transform>()
                    .Where(x => x.transform != transform)
                    .Select(x => x.position)
                    .ToArray(), alpha, planeDistanceTolerance);
            
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }
}
