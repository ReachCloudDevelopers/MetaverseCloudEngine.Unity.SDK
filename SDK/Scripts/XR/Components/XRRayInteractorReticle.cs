#if MV_XR_TOOLKIT
using TriInspectorMVCE;
using UnityEngine;

#if MV_XR_TOOLKIT_3
using XRRayInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor;
#else
using XRRayInteractor = UnityEngine.XR.Interaction.Toolkit.XRRayInteractor;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [RequireComponent(typeof(XRRayInteractor))]
    public class XRRayInteractorReticle : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField] private GameObject reticle;
        
        private XRRayInteractor _rayInteractor;

        private void Awake()
        {
            _rayInteractor = GetComponent<XRRayInteractor>();
            if (!_rayInteractor)
                enabled = false;
        }

        private void OnDisable()
        {
            if (reticle)
                reticle.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_rayInteractor.TryGetCurrent3DRaycastHit(out var hit))
            {
                reticle.SetActive(true);
                reticle.transform.position = hit.point;
                reticle.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
            }
            else if (_rayInteractor.TryGetCurrentUIRaycastResult(out var hit2D))
            {
                reticle.SetActive(true);
                reticle.transform.position = hit2D.worldPosition;
                reticle.transform.rotation = Quaternion.LookRotation(hit2D.worldNormal, Vector3.up);
            }
            else
                reticle.SetActive(false);
        }
    }
}
#endif