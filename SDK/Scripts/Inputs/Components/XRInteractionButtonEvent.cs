using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.XR.Components;
using System;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

#if MV_XR_TOOLKIT_3
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
using IXRInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor;
using IXRSelectInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor;
using IXRHoverInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRHoverInteractor;
#else
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable;
using IXRInteractor = UnityEngine.XR.Interaction.Toolkit.IXRInteractor;
using IXRSelectInteractor = UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractor;
using IXRHoverInteractor = UnityEngine.XR.Interaction.Toolkit.IXRHoverInteractor;
#endif

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    [DeclareFoldoutGroup("Hand Options")]
    [DeclareFoldoutGroup("Events")]
    [HideMonoScript]
    [Experimental]
    public class XRInteractionButtonEvent : TriInspectorMonoBehaviour
    {
        [Flags]
        public enum DeviceIndex
        {
            First = 1,
            Second = 2,
        }

        [InfoBox("Use this component to create special button actions for XR interactable objects.")]
        [DisallowNull] public XRBaseInteractable interactable;
        public InputHelpers.Button[] buttons = { InputHelpers.Button.TriggerButton };
        [Group("Hand Options")] public DeviceIndex deviceIndex = DeviceIndex.First;
        [Group("Hand Options")] public bool allowLeftHand = true;
        [Group("Hand Options")] public bool allowRightHand = true;
        [Group("Hand Options")] public bool listenOnHover;
        [Group("Events")] public UnityEvent onPressed;
        [Group("Events")] public UnityEvent onReleased;

        private readonly List<InputDevice> _devices = new();
        private readonly List<IXRInteractor> _interactors = new();
        private bool _pressed;

        private void Reset()
        {
            FindInteractable();
        }

        private void Awake()
        {
            if (buttons is null || buttons.Length == 0)
            {
                enabled = false;
                return;
            }

            FindInteractable();

            if (!interactable)
            {
                enabled = false;
                return;
            }

            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDestroy()
        {
            if (!interactable) return;
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        private void Update() => DetectInput();

        private void DetectInput()
        {
            if (_devices.Count == 0 || !XRSettings.isDeviceActive)
            {
                if (!_pressed) return;
                onReleased?.Invoke();
                _pressed = false;
                return;
            }
            
            for (var i = _devices.Count - 1; i >= 0; i--)
            {
                if (!listenOnHover && _interactors[i] is IXRSelectInteractor selector && !selector.IsSelecting(interactable))
                {
                    _devices.Remove(_devices[i]);
                    _interactors.Remove(selector);
                    if (_pressed)
                    {
                        onReleased?.Invoke();
                        _pressed = false;
                    }
                    break;
                }
                
                if (listenOnHover && _interactors[i] is IXRHoverInteractor hover && !hover.IsHovering(interactable))
                {
                    _devices.Remove(_devices[i]);
                    _interactors.Remove(hover);
                    if (_pressed)
                    {
                        onReleased?.Invoke();
                        _pressed = false;
                    }
                    break;
                }
                
                if (!deviceIndex.HasFlag(DeviceIndex.First) && i == 0)
                    continue;
                if (!deviceIndex.HasFlag(DeviceIndex.Second) && i == 1)
                    continue;
                
                for (var j = buttons.Length - 1; j >= 0; j--)
                {
                    if (_devices[i].IsPressed(buttons[j], out var pressed) && pressed)
                    {
                        if (_pressed) continue;
                        onPressed?.Invoke();
                        _pressed = true;
                    }
                    else if (_pressed)
                    {
                        onReleased?.Invoke();
                        _pressed = false;
                    }
                }
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!listenOnHover) AddDevice(args.interactorObject);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (!listenOnHover) RemoveDevice(args.interactorObject);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (listenOnHover) AddDevice(args.interactorObject);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (listenOnHover) RemoveDevice(args.interactorObject);
        }

        private void AddDevice(IXRInteractor interactor)
        {
            var controller = interactor.transform.GetComponent<MetaverseXRController>();
            if (controller is null || 
                ((!allowLeftHand || controller.Hand is not MetaverseXRController.HandType.Left) &&
                 (!allowRightHand || controller.Hand is not MetaverseXRController.HandType.Right)))
                return;
            var device = InputDevices.GetDeviceAtXRNode(controller.XRNode);
            if (!device.isValid) return;
            _devices.Add(device);
            _interactors.Add(interactor);
        }

        private void RemoveDevice(IXRInteractor interactor)
        {
            var controller = interactor.transform.GetComponent<MetaverseXRController>();
            if (!controller) return;
            _devices.Remove(InputDevices.GetDeviceAtXRNode(controller.XRNode));
            _interactors.Remove(interactor);
        }

        private void FindInteractable()
        {
            if (!interactable) interactable = GetComponentInParent<XRBaseInteractable>(true);
        }
    }
}
