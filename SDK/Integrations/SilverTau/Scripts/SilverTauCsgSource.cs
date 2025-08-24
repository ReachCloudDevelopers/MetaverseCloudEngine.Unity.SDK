using System.Runtime.CompilerServices;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Unity.Async;
#if MV_PROBUILDER
using UnityEngine.ProBuilder.Csg;
#endif

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

        [Tooltip("If true, will disable this object's renderer after carving is complete.")]
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
                if (_landPlot) _landPlot.events.onLoadSuccess.AddListener(Carve);
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

                var bounds = gameObject.GetVisibleBounds();
                if (bounds.size.sqrMagnitude <= 0)
                    return;

                var carved = false;

                var count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, OverlapResults, transform.rotation);
                for (var i = 0; i < count; i++)
                {
                    var col = OverlapResults[i];
                    if (!col) continue;

                    var csgTarget = col.GetComponent<SilverTauCsgTarget>();
                    if (!csgTarget) continue;
                    if (csgTarget.transform.IsChildOf(transform)) continue;

#if MV_PROBUILDER
                    var targetGo = col.gameObject;
                    var brushGo = gameObject;

                    // Need target mesh & renderer for materials/layer/static flags
                    var targetFilter = targetGo.GetComponent<MeshFilter>();
                    var targetRenderer = targetGo.GetComponent<MeshRenderer>();
                    if (!targetFilter || !targetRenderer)
                        continue;

                    Model resultModel = null;
                    try
                    {
                        // Target = Target - Brush
                        resultModel = CSG.Subtract(targetGo, brushGo);
                    }
                    catch
                    {
                        continue; // Skip on pb_CSG failure
                    }

                    if (resultModel == null || resultModel.mesh == null || resultModel.mesh.vertexCount == 0)
                        continue;

                    // --- Create a NEW GameObject with MF/MR/MC ---
                    var parent = targetGo.transform;
                    var resultName = $"{targetGo.name}_Carved";

                    var resultGo = new GameObject(resultName);
                    // Parent first, then set world-space transform to (0,0,0)
                    resultGo.transform.SetParent(parent, worldPositionStays: true);
                    resultGo.transform.position = Vector3.zero;                     // world zero
                    resultGo.transform.rotation = Quaternion.identity;              // world identity
                    resultGo.transform.localScale = Vector3.one;                    // unit scale

                    // Copy a few useful flags
                    resultGo.layer = targetGo.layer;
#if UNITY_EDITOR
                    resultGo.isStatic = targetGo.isStatic;
#endif

                    var mf = resultGo.AddComponent<MeshFilter>();
                    var mr = resultGo.AddComponent<MeshRenderer>();
                    mf.sharedMesh = resultModel.mesh;
                    targetRenderer.enabled = false;

                    // Prefer CSG materials; if none, fallback to target's current materials
                    if (resultModel.materials != null && resultModel.materials.Count > 0)
                        mr.sharedMaterials = resultModel.materials.ToArray();
                    else
                        mr.sharedMaterials = targetRenderer.sharedMaterials;

                    var mc = resultGo.AddComponent<MeshCollider>();
                    mc.sharedMesh = resultModel.mesh;
                    
                    if (targetRenderer.TryGetComponent(out Collider targetMc))
                    {
                        targetMc.enabled = false;
                        mc.gameObject.layer = targetMc.gameObject.layer;
                        mc.includeLayers = targetMc.includeLayers;
                        mc.excludeLayers = targetMc.excludeLayers;
                        if (targetMc.isTrigger)
                        {
                            mc.isTrigger = true;
                            mc.convex = true;
                        }
                    }

                    // If original target was dynamic, Unity requires convex MeshCollider
                    if (targetGo.TryGetComponent<Rigidbody>(out _))
                        mc.convex = true;

                    resultGo.gameObject.AddComponent<SilverTauCsgTarget>();
                    
                    carved = true;
#else
                    MetaverseProgram.Logger.LogWarning("pb_CSG is not enabled. Define MV_PARABOX_CSG and add Parabox CSG to use this feature.");
#endif
                }

                if (carved)
                {
                    if (disableObjectAfterCarve && _meshRenderer)
                        _meshRenderer.enabled = false;

                    onCarveSuccess?.Invoke();
                }
                else
                {
                    onCarveFailure?.Invoke();
                }
            });
        }
    }
}
