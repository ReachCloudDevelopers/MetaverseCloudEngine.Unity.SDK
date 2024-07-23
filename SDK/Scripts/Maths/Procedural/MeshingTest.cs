using System.Collections;
using System.Linq;
using MetaverseCloudEngine.Unity.Maths.Procedural;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    [ExecuteInEditMode]
    public class MeshingTest : MonoBehaviour
    {
        [SerializeField] private int gridSize = 32;
        [SerializeField] private float cellSize = 1;
        [SerializeField] private float isoLevel = 0.5f;
        [SerializeField, Range(0.1f, 10f)] private float performance = 1f;
        
        private bool _generating;
        private IEnumerator _operation;
        
        private void Update()
        {
            if (_generating)
            {
                if (Application.isEditor && !Application.isPlaying) 
                {
                    if (_operation is null)
                    {
                        _generating = false;
                        return;
                    }
                    _operation.MoveNext(); 
                    return;
                }
                
                return;
            }
            
            _operation = MeshingAPI.GenerateMarchingCubes(
                GetComponentsInChildren<Transform>()
                    .Where(x => x.transform != transform)
                    .Select(x => x.localPosition) 
                    .ToArray(),
                m =>
                {
                    GetComponent<MeshFilter>().sharedMesh = m;
                    _generating = false;
                },
                maxIterationsPerFrame: (int)(5000 * performance),
                gridSize: gridSize,
                gridScale: cellSize,
                isoLevel: isoLevel,
                mesh: GetComponent<MeshFilter>().sharedMesh);

            if (Application.isPlaying)
                StartCoroutine(_operation);

            _generating = true;
        }
    }
}
