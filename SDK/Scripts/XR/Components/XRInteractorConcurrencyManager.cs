using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// When multiple XR Interactors are within a single scene (possibly if a main menu contains a separate XR rig)
    /// we want to make sure we disable interactions on the child so as to prevent selection and hovering
    /// when another rig is enabled.
    /// </summary>
    [HideMonoScript]
    public class XRInteractorConcurrencyManager : TriInspectorMonoBehaviour
    {
        [Tooltip("If this is left blank, the children will be searched for interactors.")]
        public XRBaseInteractor[] interactors;
        [Tooltip("The priority of this interactor. The higher the number, the higher the priority.")]
        public int priority;

        private bool _isInteractionEnabled = true;
        private Dictionary<XRRayInteractor, LayerMask> _disabledRayInteractors;
        private static List<XRInteractorConcurrencyManager> _list = new();
        private XRSelectFilterDelegate _preventSelectDelegate;
        private XRHoverFilterDelegate _preventHoverDelegate;

        private static event Action ListChanged;

        private void Awake()
        {
            if (interactors == null || interactors.Length == 0)
                interactors = GetComponentsInChildren<XRBaseInteractor>(true);
            
            _preventSelectDelegate = new XRSelectFilterDelegate(CanSelect);
            _preventHoverDelegate = new XRHoverFilterDelegate(CanHover);

            ListChanged += OnListChanged;
        }

        private void Start()
        {
            foreach (var interactor in interactors)
            {
                interactor.selectFilters.Add(_preventSelectDelegate);
                interactor.hoverFilters.Add(_preventHoverDelegate);
            }
        }

        private void OnDestroy()
        {
            ListChanged -= OnListChanged;
        }

        private void OnEnable()
        {
            Add();
        }

        private void OnDisable()
        {
            Remove();
        }

        private void Add()
        {
            _list.Add(this);
            _list = _list.OrderBy(x => x.priority).ToList();
            ListChanged?.Invoke();
        }

        private void Remove()
        {
            _list.Remove(this);
            _list = _list.OrderBy(x => x.priority).ToList();
            ListChanged?.Invoke();
        }

        private void OnListChanged()
        {
            _isInteractionEnabled = _list.Count == 0 || _list[^1] == this;
            
            foreach (var interactor in interactors)
            {
                if (interactor is not XRRayInteractor rayInteractor)
                    continue;

                _disabledRayInteractors ??= new Dictionary<XRRayInteractor, LayerMask>(); 
                if (!_isInteractionEnabled)
                {
                    if (_disabledRayInteractors.ContainsKey(rayInteractor))
                        continue;
                    
                    var hasUiLayerInRaycastLayer = 
                        rayInteractor.raycastMask == (rayInteractor.raycastMask | (1 << LayerMask.NameToLayer("UI"))) ||
                        rayInteractor.raycastMask == (rayInteractor.raycastMask | (1 << LayerMask.NameToLayer("System UI")));
                    if (!hasUiLayerInRaycastLayer)
                        continue;
                    
                    _disabledRayInteractors[rayInteractor] = rayInteractor.raycastMask;
                    rayInteractor.raycastMask &= ~(1 << LayerMask.NameToLayer("UI"));
                    rayInteractor.raycastMask &= ~(1 << LayerMask.NameToLayer("System UI"));
                }
                else if (_disabledRayInteractors.Remove(rayInteractor, out var originalLayerMask))
                {
                    rayInteractor.raycastMask = originalLayerMask;
                }
            }
        }

        private bool CanSelect(
            IXRSelectInteractor xrSelectInteractor,
            IXRSelectInteractable xrSelectInteractable)
        {
            return _isInteractionEnabled;
        }

        private bool CanHover(
            IXRHoverInteractor xrHoverInteractor,
            IXRHoverInteractable xrHoverInteractable)
        {
            return _isInteractionEnabled;
        }
    }
}