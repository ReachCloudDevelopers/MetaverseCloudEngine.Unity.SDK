using System;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    public class NetworkEvent : MetaSpaceBehaviour
    {
        [Required]
        [Tooltip("The unique identifier of this event.")]
        [SerializeField] private string eventID;
        [Tooltip("The receivers of this event.")]
        [SerializeField] private NetworkMessageReceivers receivers;
        [Tooltip("Whether this event should be buffered.")]
        [SerializeField] private bool buffered;
        [Tooltip("The event that will be invoked when this event is received.")]
        [SerializeField] private UnityEvent onNetworkEvent;
        [Tooltip("The event that will be invoked when this event is received, containing the sender's user name.")]
        [SerializeField] private UnityEvent<string> onNetworkEventWithSender;

        /// <summary>
        /// The unique identifier of this event.
        /// </summary>
        public string EventID
        {
            get => eventID;
            set => eventID = value;
        }

        /// <summary>
        /// The receivers of this event.
        /// </summary>
        public NetworkMessageReceivers Receivers
        {
            get => receivers;
            set => receivers = value;
        }
        
        /// <summary>
        /// The receivers of this event.
        /// </summary>
        public int ReceiversInt
        {
            get => (int)receivers;
            set => receivers = (NetworkMessageReceivers)value;
        }

        /// <summary>
        /// Whether this event should be buffered.
        /// </summary>
        public bool Buffered
        {
            get => buffered;
            set => buffered = value;
        }

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();

            if (!string.IsNullOrEmpty(eventID))
                MetaSpace.NetworkingService?.AddEventHandler(
                    (short)NetworkEventType.NetworkEventBehavior,
                    OnNetworkEventBehaviorEvent);
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            base.OnMetaSpaceBehaviourDestroyed();

            MetaSpace.NetworkingService?.RemoveEventHandler(
                (short)NetworkEventType.NetworkEventBehavior,
                OnNetworkEventBehaviorEvent);
        }

        /// <summary>
        /// Sends this event to the receivers specified in the <see cref="receivers"/> field.
        /// </summary>
        public void SendEvent()
        {
            if (!isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(eventID)) return;
            MetaSpace.NetworkingService?.InvokeEvent(
                (short)NetworkEventType.NetworkEventBehavior,
                receivers,
                buffered,
                eventID);
        }
        
        /// <summary>
        /// Removes this event from the buffer.
        /// </summary>
        public void RemoveBufferedEvent()
        {
            if (!isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(eventID)) return;
            MetaSpace.NetworkingService?.RemoveEventFromBuffer(
                (short)NetworkEventType.NetworkEventBehavior,
                eventID);
        }

        private void OnNetworkEventBehaviorEvent(short eventId, int playerId, object content)
        {
            if (!isActiveAndEnabled) 
                return;
            if (content is not string id || id != eventID) 
                return;
            
            if (MetaSpace && MetaSpace.NetworkingService is not null)
                MetaSpace.NetworkingService.GetPlayerName(playerId, playerName => OnNetworkEvent(playerName));
            else
            {
                OnNetworkEvent();
            }
        }

        private void OnNetworkEvent(string userName = "")
        {
            try
            {
                onNetworkEvent?.Invoke();
                onNetworkEventWithSender?.Invoke(userName ?? string.Empty);
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
            }
        }
    }
}