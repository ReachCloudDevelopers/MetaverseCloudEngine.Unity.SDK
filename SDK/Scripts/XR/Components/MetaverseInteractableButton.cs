#if MV_XR_TOOLKIT
using TriInspectorMVCE;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Events;
using UnityEngine;

#if MV_XR_TOOLKIT_3
using XRSimpleInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable;
#else
using XRSimpleInteractable = UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [DisallowMultipleComponent]
    [HideMonoScript]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class MetaverseInteractableButton : TriInspectorMonoBehaviour
    {
        public UnityEvent onClicked;
        
        private XRSimpleInteractable _simpleInteractable;

        private void OnValidate()
        {
            InitInteractable();
        }

        private void Awake()
        {
            InitInteractable();
            _simpleInteractable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnEnable()
        {
            _simpleInteractable.enabled = true;
        }

        private void OnDisable()
        {
            _simpleInteractable.enabled = false;
        }

        private void InitInteractable()
        {
            _simpleInteractable = gameObject.GetOrAddComponent<XRSimpleInteractable>();
            if (_simpleInteractable.colliders.Count == 0)
                _simpleInteractable.colliders.AddRange(GetComponentsInChildren<Collider>(true));
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (isActiveAndEnabled && MVUtils.CachedTime >= MetaverseInteractable.GlobalInteractionCooldownTime)
                onClicked?.Invoke();
            MetaverseInteractable.InteractCooldown();
            args.manager.CancelInteractableSelection(args.interactableObject);
        }
    }
}
#endif