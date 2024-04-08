using TMPro;
using TriInspectorMVCE;
using UnityEngine;
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
        [Header("UI (Optional)")]
        [SerializeField] private TMP_Text interactionText;
        [SerializeField] private string defaultInteractionText = "Interact";
        
        private XRDirectInteractor _directInteractor;
        private Transform _target;
        private bool _hasTarget;
        private bool _enabledIndicator;
        private float _nextUpdateTime;

        private void Awake()
        {
            _directInteractor = GetComponent<XRDirectInteractor>();
            if (!_directInteractor)
                enabled = false;
        }

        private void LateUpdate()
        {
            try
            {
                if (MVUtils.CachedTime >= _nextUpdateTime)
                {
                    _nextUpdateTime += UpdateInterval;
                    _target = null;
                    if (_directInteractor.hasSelection)
                    {
                        if (_directInteractor.interactablesHovered.Count > 0)
                            _target = _directInteractor.interactablesHovered[0].transform;
                    }
                    else
                    {
                        for (var i = 0; i < _directInteractor.targetsForSelection.Count; i++)
                        {
                            var xrSelectInteractable = _directInteractor.targetsForSelection[i];
                            if (xrSelectInteractable.isSelected) continue;
                            _target = xrSelectInteractable.transform;
                            break;
                        }
                    }
                    _hasTarget = _target;
                }

                if (_hasTarget && _directInteractor.allowHover && _directInteractor.hasHover)
                {
                    if (!_enabledIndicator)
                    {
                        if (indicator)
                            indicator.SetActive(true);
                        _enabledIndicator = true;
                    }
                    indicator.transform.position = _target!.position;
                    if (_target.TryGetComponent(out XRInteractionMetadata metadata))
                    {
                        if (interactionText)
                            interactionText.text = metadata.interactionText;
                    }
                    else if (interactionText)
                    {
                        interactionText.text = defaultInteractionText;
                    }
                    _enabledIndicator = true;
                }
                else if (_enabledIndicator)
                {
                    if (indicator)
                        indicator.SetActive(false);
                    _enabledIndicator = false;
                }
            }
            catch
            {
                _hasTarget = false;
            }
        }
    }
}