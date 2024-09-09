using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [RequireComponent(typeof(XRDirectInteractor))]
    [DefaultExecutionOrder(int.MaxValue)]
    public class XRDirectInteractorIndicator : TriInspectorMonoBehaviour
    {
        private const float UpdateInterval = 1f / 5;
        
        [Required]
        [SerializeField] private GameObject indicator;
        [SerializeField] private bool allowHoverWhileSelected;
        [SerializeField] private UnityEvent<string> onInteractionText;
        [SerializeField] private string defaultInteractionText = "Interact";
        [SerializeField] private UnityEvent<Transform> onTargetChanged;
        [SerializeField] private UnityEvent onTargetLost;

        private XRDirectInteractor _directInteractor;
        private Transform _target;
        private bool _hasTarget;
        private bool _enabledIndicator;
        private float _nextUpdateTime;
        private Vector3 _indicatorRelativePos;

        private void Awake()
        {
            _directInteractor = GetComponent<XRDirectInteractor>();
            if (!_directInteractor)
                enabled = false;
            onInteractionText ??= new UnityEvent<string>();
            onInteractionText?.Invoke("");
        }

        private void OnDisable()
        {
            if (indicator)
                indicator.SetActive(false);
            _enabledIndicator = false;
            onInteractionText?.Invoke("");
        }

        private void LateUpdate()
        {
            try
            {
                if (MVUtils.CachedTime >= _nextUpdateTime)
                {
                    _nextUpdateTime += UpdateInterval;
                    Transform newTarget = null;
                    try
                    {
                        if (_directInteractor.hasSelection)
                        {
                            if (allowHoverWhileSelected && _directInteractor.interactablesHovered.Count > 0)
                            {
                                var potentialTarget =
                                    _directInteractor.interactablesHovered.FirstOrDefault(x =>
                                        x is IXRSelectInteractable s && s.IsSelectableBy(_directInteractor) && (!s.isSelected || (s.selectMode == InteractableSelectMode.Multiple && s.interactorsSelecting.Count == 1)));
                                if (potentialTarget != null)
                                    newTarget = potentialTarget.transform;
                            }
                        }
                        else
                        {
                            foreach (var xrSelectInteractable in 
                                     _directInteractor.targetsForSelection
                                         .Where(xrSelectInteractable => xrSelectInteractable.IsSelectableBy(_directInteractor) && !xrSelectInteractable.isSelected))
                            {
                                newTarget = xrSelectInteractable.transform;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        if (_target != newTarget)
                        {
                            _target = newTarget;
                            _hasTarget = _target;
                            if (_hasTarget)
                            {
                                _enabledIndicator = false;
                                onTargetChanged?.Invoke(_target);
                            }
                            else
                                onTargetLost?.Invoke();
                        }
                    }
                }

                if (_hasTarget && _directInteractor.allowHover && _directInteractor.hasHover)
                {
                    if (!_enabledIndicator)
                    {
                        _indicatorRelativePos = CalculateTargetRelativeBounds();
                        if (indicator)
                            indicator.SetActive(true);
                        _enabledIndicator = true;
                    }
                    indicator.transform.position = _target!.TransformPoint(_indicatorRelativePos);
                    onInteractionText?.Invoke(_target.TryGetComponent(out XRInteractionMetadata metadata)
                        ? metadata.interactionText
                        : defaultInteractionText);
                    _enabledIndicator = true;
                }
                else if (_enabledIndicator)
                {
                    if (indicator)
                        indicator.SetActive(false);
                    _enabledIndicator = false;
                    onInteractionText?.Invoke("");
                }
            }
            catch
            {
                _hasTarget = false;
            }
        }

        private Vector3 CalculateTargetRelativeBounds()
        {
            if (!_target.TryGetComponent(out IXRInteractable interactable))
                return Vector3.zero;

            var colliders = interactable.colliders;
            if (colliders == null || colliders.Count == 0)
                return Vector3.zero;
            
            var bounds = new Bounds(colliders[0].transform.position, Vector3.zero);
            foreach (var col in colliders)
                bounds.Encapsulate(col.bounds);
            
            return _target.InverseTransformPoint(bounds.center);
        }
    }
}