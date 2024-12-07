#if MV_XR_TOOLKIT
using System;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class Sitter : NetworkObjectBehaviour
    {
        [Serializable]
        public class SitterEvents
        {
            public UnityEvent<Seat> onSeatEntering;
            public UnityEvent<Seat> onSeatEntered;
            public UnityEvent<Seat> onSeatExiting;
            public UnityEvent onSeatExited;
        }

        [Serializable]
        public class SitterInputEvents
        {
            [Header("Input")]
            public UnityEvent onAllowExitInput;
            public UnityEvent onDisallowExitInput;

            public void Invoke(bool value)
            {
                if (value) onAllowExitInput?.Invoke();
                else onDisallowExitInput?.Invoke();
            }
        }

        [SerializeField] private Animator animator;
        [SerializeField] private SitterEvents events;
        [SerializeField] private SitterEvents localOnlyEvents;
        [SerializeField] private SitterInputEvents localInputEvents;
        [SerializeField] private SitterEvents remoteOnlyEvents;

        public Seat CurrentSeat { get; private set; }
        public Transform Root => NetworkObject ? NetworkObject.transform : transform;
        public Animator Animator => animator;
        public bool Destroying { get; private set; }

        private void Reset()
        {
            animator = this.GetNearestComponent<Animator>();
        }

        protected override void Awake()
        {
            base.Awake();
            AddLocalAndRemoteEventHandlers();
        }

        private void AddLocalAndRemoteEventHandlers()
        {
            events.onSeatExiting.AddListener(seat =>
            {
                if (NetworkObject.IsStateAuthority) localOnlyEvents.onSeatExiting?.Invoke(seat);
                else remoteOnlyEvents.onSeatExiting?.Invoke(seat);
            });

            events.onSeatExited.AddListener(() =>
            {
                if (NetworkObject.IsStateAuthority) localOnlyEvents.onSeatExited?.Invoke();
                else remoteOnlyEvents.onSeatExited?.Invoke();
            });

            events.onSeatEntering.AddListener(seat =>
            {
                if (NetworkObject.IsStateAuthority) localOnlyEvents.onSeatEntering?.Invoke(seat);
                else remoteOnlyEvents.onSeatEntering?.Invoke(seat);
            });

            events.onSeatEntered.AddListener(seat =>
            {
                if (NetworkObject.IsStateAuthority) localOnlyEvents.onSeatEntered?.Invoke(seat);
                else remoteOnlyEvents.onSeatEntered?.Invoke(seat);
            });
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            Destroying = true;
            OnSeatChanged(null);
        }

        private void OnEnable()
        {
            OnTransformParentChanged();
        }

        private void OnTransformParentChanged()
        {
            var seat = GetComponentInParent<Seat>();
            OnSeatChanged(seat);
        }

        public void ExitCurrentSeat()
        {
            if (CurrentSeat && CurrentSeat.AllowExitInput)
                CurrentSeat.Exit();
        }

        public void ForceExitCurrentSeat()
        {
            if (CurrentSeat)
                CurrentSeat.Exit();
        }

        private void OnSeatChanged(Seat seat)
        {
            if (seat == CurrentSeat)
                return;

            if (CurrentSeat != null)
            {
                Seat oldSeat = CurrentSeat;
                CurrentSeat = null;
                events.onSeatExiting?.Invoke(oldSeat);
                oldSeat.NotifyExited(this);
                events.onSeatExited?.Invoke();
                AllowExitInputChanged();
            }

            if (seat != null)
            {
                events.onSeatEntering?.Invoke(seat);
                CurrentSeat = seat;
                CurrentSeat.NotifyEntered(this);
                events.onSeatEntered?.Invoke(CurrentSeat);
                AllowExitInputChanged();
            }
        }

        public void AllowExitInputChanged()
        {
            localInputEvents.Invoke(CurrentSeat && CurrentSeat.AllowExitInput);
        }
    }
}
#endif