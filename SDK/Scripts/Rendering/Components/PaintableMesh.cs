using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [RequireComponent(typeof(Renderer))]
    public class PaintableMesh : MonoBehaviour
    {
        [Min(0)] public int materialIndex = 0;
        public string mainTexture = "_MainTex";
        public UnityEvent onPainted;
    }
}