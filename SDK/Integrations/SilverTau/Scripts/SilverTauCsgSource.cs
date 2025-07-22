using SilverTau.Utilities;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    [HideMonoScript]
    public class SilverTauCsgSource : TriInspectorMonoBehaviour
    {
        [Tooltip("If true, will call the 'Carve' method on start.")]
        [SerializeField] private bool carveOnStart = true;
        [Tooltip("Invoked if any CSG operation was successful.")]
        [SerializeField] private UnityEvent onCarveSuccess = new();
        [Tooltip("Invoked if no CSG operation was successful.")]
        [SerializeField] private UnityEvent onCarveFailure = new();
        
        public UnityEvent OnCarveSuccess => onCarveSuccess;
        public UnityEvent OnCarveFailure => onCarveFailure;
        
        private static readonly Collider[] OverlapResults = new Collider[100];
        
        private void Start()
        {
            if (carveOnStart)
                Carve();
        }
        
        public void Carve()
        {
            var bounds = gameObject.GetVisibleBounds();
            if (bounds.size.sqrMagnitude <= 0)
                return;
            var carved = false;
            var size = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents * 0.5f, OverlapResults, transform.rotation);
            for (var index = 0; index < size; index++)
            {
                var col = OverlapResults[index];
                var csg = col.GetComponentInParent<SilverTauCsgTarget>();
                if (!csg) continue;
                if (csg.transform.IsChildOf(transform)) continue;
                CSGeometry.Subtractive(csg.gameObject, new[] { gameObject });
                carved = true;
            }
            if (carved) onCarveSuccess?.Invoke();
            else onCarveFailure?.Invoke();
        }
    }
}