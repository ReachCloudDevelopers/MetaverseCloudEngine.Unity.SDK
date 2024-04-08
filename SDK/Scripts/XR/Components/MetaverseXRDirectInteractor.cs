using System.Linq;
using MetaverseCloudEngine.Unity.Inputs.Components;
using MetaverseCloudEngine.Unity.Locomotion.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class MetaverseXRDirectInteractor : XRDirectInteractor
    {
        [Header("Behavior")]
        [Tooltip("If true, the interactor will not be able to select anything while the user is sitting.")]
        [SerializeField] private bool blockWhileSitting;
        [Tooltip("If true, requires the interact player input flag to be enabled in order to select.")]
        [SerializeField] private bool requireInteractInputFlag = true;
        
        [Header("Extra Events")]
        [Tooltip("Invoked when the first selection is entered.")]
        [SerializeField] private UnityEvent<IXRSelectInteractable> firstSelectionEntered;
        [Tooltip("Invoked when the last selection is exited.")]
        [SerializeField] private UnityEvent<IXRSelectInteractable> lastSelectionExited;

        [Header("Input")]
        [Tooltip("If set, the interactor will deselect the current selection when this input is triggered.")]
        [SerializeField] private InputActionProperty forceDeselectInput;

        private bool _forceDeselect;
        private bool _useCustomDeselectInput;
        private Sitter _sitter;
        
        public override bool isSelectActive
        {
            get
            {
                if (_forceDeselect) return false;
                return (hasSelection && selectActionTrigger > InputTriggerType.StateChange) || base.isSelectActive;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            
            if (forceDeselectInput.action != null && forceDeselectInput.action.bindings.Any())
            {
                forceDeselectInput.action.Enable();
                forceDeselectInput.action.performed += _ => ForceDeselect(false);
                _useCustomDeselectInput = true;
            }
        }

        protected override void Start()
        {
            base.Start();
            
            _sitter = GetComponentInParent<Sitter>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            forceDeselectInput.action?.Dispose();
        }

        /// <summary>
        /// Forces the interactor to deselect the current selection.
        /// </summary>
        public void ForceDeselect(bool dropCompletely = true)
        {
            if (!hasSelection)
                return;

            _forceDeselect = true;

            if (dropCompletely) 
                return;

            var metaverseInteractables =
                interactablesSelected
                    .Select(x => x.transform.GetComponent<MetaverseInteractable>())
                    .Where(x => x)
                    .ToArray();

            foreach (var metaverseInteractable in metaverseInteractables)
                metaverseInteractable.MoveToSocketOrDeselect();
        }

        protected override void OnSelectEntering(SelectEnterEventArgs args)
        {
            if (args.interactableObject.transform.GetComponent<MetaverseInteractableButton>())
                return;

            if (args.interactableObject.transform.TryGetComponent(out MetaverseInteractable metaverseInteractable))
            {
                // Deselect all other metaverse interactables
                if (interactablesSelected.Count > 0)
                {
                    var metaverseInteractables =
                        interactablesSelected
                            .Select(x => x.transform.GetComponent<MetaverseInteractable>())
                            .Where(x => x && metaverseInteractable != x)
                            .ToArray();

                    foreach (var interactable in metaverseInteractables)
                        interactable.MoveToSocketOrDeselect();
                }
            }

            base.OnSelectEntering(args);

            if (interactablesSelected.Count == 1)
                firstSelectionEntered?.Invoke(args.interactableObject);
        }

        protected override void OnSelectExiting(SelectExitEventArgs args)
        {
            if (args.interactableObject.transform.GetComponent<MetaverseInteractableButton>())
                return;

            base.OnSelectExiting(args);

            _forceDeselect = false;
            if (interactablesSelected.Count == 0)
                lastSelectionExited?.Invoke(args.interactableObject);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            if (IsBlockedBySitting())
                return false;
            
            if (requireInteractInputFlag && !PlayerInputAPI.InteractInputEnabled)
                return IsSelecting(interactable);

            if (selectActionTrigger <= InputTriggerType.StateChange) 
                return base.CanSelect(interactable);
            
            var selectActivatedThisFrame = xrController.selectInteractionState.activatedThisFrame;
            if (selectActivatedThisFrame && !_useCustomDeselectInput && interactablesHovered.Count == 0)
                return false;
            
            return selectActivatedThisFrame || base.CanSelect(interactable);
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            if (IsBlockedBySitting())
                return false;
            if (requireInteractInputFlag && !PlayerInputAPI.InteractInputEnabled)
                return false;
            if (hasSelection)
                return !IsSelecting(interactable);
            return base.CanHover(interactable);
        }

        private bool IsBlockedBySitting() =>
            blockWhileSitting && _sitter && _sitter.CurrentSeat;
    }
}