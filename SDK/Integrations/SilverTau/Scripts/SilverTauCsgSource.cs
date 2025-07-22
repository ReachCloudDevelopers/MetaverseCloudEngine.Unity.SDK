using MetaverseCloudEngine.Unity.Assets.LandPlots;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Unity.Async;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    [HideMonoScript]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SilverTauCsgSource : TriInspectorMonoBehaviour
    {
        [Tooltip("If true, will call the 'Carve' method on start.")]
        [SerializeField] private bool carveOnStart = true;
        [Tooltip("If true, will call the 'Carve' method when the land plot loads.")]
        [SerializeField] private bool carveOnLandPlotLoad = true;
        [Tooltip("If true, will disable this object after carving is complete.")]
        [SerializeField] private bool disableObjectAfterCarve = true;
        [Tooltip("Invoked if any CSG operation was successful.")]
        [SerializeField] private UnityEvent onCarveSuccess = new();
        [Tooltip("Invoked if no CSG operation was successful.")]
        [SerializeField] private UnityEvent onCarveFailure = new();
        
        private LandPlot _landPlot;
        private MeshRenderer _meshRenderer;
        
        public UnityEvent OnCarveSuccess => onCarveSuccess;
        public UnityEvent OnCarveFailure => onCarveFailure;
        
        private static readonly Collider[] OverlapResults = new Collider[100];
        
        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            
            if (carveOnLandPlotLoad)
            {
                _landPlot = GetComponentInParent<LandPlot>();
                _landPlot.events.onLoadSuccess.AddListener(Carve);
            }
            
            if (carveOnStart)
                Carve();
        }

        private void OnDestroy()
        {
            if (_landPlot) _landPlot.events.onLoadSuccess.RemoveListener(Carve);
        }

        public void Carve()
        {
            MetaverseDispatcher.AtEndOfFrame(() => 
			{
				if (!this || !isActiveAndEnabled)
                    return;
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                var bounds = gameObject.GetVisibleBounds();
                if (bounds.size.sqrMagnitude <= 0)
                    return;
                var carved = false;
                var size = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents * 0.5f, OverlapResults, transform.rotation);
                for (var index = 0; index < size; index++)
                {
                    var col = OverlapResults[index];
                    var csgTarget = col.GetComponent<SilverTauCsgTarget>();
                    if (!csgTarget) continue;
                    if (csgTarget.transform.IsChildOf(transform)) continue;
                    MeshMakerNamespace.CSG.EPSILON = 1e-5f;
                    var csgOp = new MeshMakerNamespace.CSG
                    {
                        Brush = gameObject,
                        Target = col.gameObject,
                        OperationType = MeshMakerNamespace.CSG.Operation.Subtract,
                        useCustomMaterial = false,
                        hideGameObjects = false,
                        keepSubmeshes = true
                    };
                    var newObj = csgOp.PerformCSG();
                    if (newObj.TryGetComponent(out MeshFilter mf))
                    {
                        var newMesh = mf.sharedMesh;
                        if (col.gameObject.TryGetComponent(out mf))
                            mf.sharedMesh = newMesh;
                        if (col is MeshCollider m)
                            m.sharedMesh = newMesh;
                        else
                        {
                            col.enabled = false;
                            var mc = col.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = newMesh;
                            if (col.gameObject.TryGetComponent(out Rigidbody rb))
                                mc.convex = true;
                        }
                    }
                    UnityEngine.Object.Destroy(newObj);
                    carved = true;
                }
                if (carved) 
                {
                    if (disableObjectAfterCarve)
                        _meshRenderer.enabled = false;
                    onCarveSuccess?.Invoke();
                }
                else onCarveFailure?.Invoke();
#endif
            });
        }
    }
}