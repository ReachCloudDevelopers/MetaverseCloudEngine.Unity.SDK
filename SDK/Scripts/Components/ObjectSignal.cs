using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using MetaverseCloudEngine.Unity.Labels;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A component that can be used to send and receive signals between objects.
    /// This can help reduce dependencies between objects, and can be used to
    /// communicate between objects without having to use a <see cref="GameObject"/> as a
    /// middleman.
    /// </summary>
    [Experimental]
    [HideMonoScript]
    public class ObjectSignal : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// A dictionary of all registered <see cref="ObjectSignal"/>s by their <see cref="Identifier"/>.
        /// </summary>
        private static Dictionary<string, List<ObjectSignal>> _registeredNotifications = new();

        /// <summary>
        /// Invoked when an <see cref="ObjectSignal"/> is registered.
        /// </summary>
        public static event Action<string> SignalRegistered;
        /// <summary>
        /// Invoked when an <see cref="ObjectSignal"/> is unregistered.
        /// </summary>
        public static event Action<string> SignalUnregistered;
        /// <summary>
        /// Invoked when an <see cref="ObjectSignal"/> is sent.
        /// </summary>
        public static event Action<string> Transmitted;

        [FormerlySerializedAs("notificationId")]
        [SerializeField, HideInInspector] private string identifier;
        [Tooltip("The ID of this object signal, used to communicate with other object signals.")]
        [SerializeField] private Label ID;

        [Tooltip("Send this notification when this object is enabled.")]
        public bool sendOnEnable;
        [Tooltip("Count this object as a receiver of the signal.")]
        public bool countAsReceiver = true;

        [Header("Events")]
        [Tooltip("Invoked when this object receives a signal.")]
        public UnityEvent onReceive = new();
        [Tooltip("Invoked when a receiver exists for this signal.")]
        public UnityEvent onReceiverExists = new();
        [Tooltip("Invoked when no receiver exists for this signal.")]
        public UnityEvent onNoReceiverExists = new();

        private bool _registered;

        /// <summary>
        /// (Write Only) Sets the unique identifier of this object signal. Used to communicate with other
        /// object signals with the same identifier.
        /// </summary>
        public string Identifier {
            set {
                ID.GetValueAsync(v =>
                {
                    if (v == value) return;
                    Unregister();
                    ID = value;
                    Register();
                });
            }
        }

        private void Awake()
        {
            Upgrade();
        }

        private void OnValidate()
        {
            Upgrade();
        }

        private void OnEnable()
        {
            Register();

            if (sendOnEnable)
                Send();
        }

        private void OnDisable() => Unregister();

        private void Upgrade()
        {
            if (!string.IsNullOrEmpty(identifier))
            {
                ID = identifier;
                identifier = null;
            }
        }

        /// <summary>
        /// Sends this object signal to all other object signals with the same identifier.
        /// </summary>
        public void Send()
        {
            Upgrade();

            ID.GetValueAsync(v =>
            {
                if (!this)
                    return;

                if (!string.IsNullOrEmpty(v))
                    Transmitted?.Invoke(v);
            });
        }

        private void OnReceive() => onReceive?.Invoke();

        private void Register()
        {
            Upgrade();

            ID.GetValueAsync(id =>
            {
                if (!this)
                    return;

                Transmitted += OnTransmitted;
                CheckForReceivers();

                if (onReceiverExists.GetPersistentEventCount() > 0 || 
                    onNoReceiverExists.GetPersistentEventCount() > 0)
                {
                    SignalRegistered += OnSignalRegistered;
                    SignalUnregistered += OnSignalUnregistered;
                }

                AddSelfToRegistry();

                SignalRegistered?.Invoke(id);
            });
        }

        private void Unregister()
        {
            Transmitted -= OnTransmitted;
            SignalRegistered -= OnSignalRegistered;
            SignalUnregistered -= OnSignalUnregistered;

            RemoveSelfFromRegistry();
        }

        private void CheckForReceivers()
        {
            List<ObjectSignal> identifiers = null;
            _registeredNotifications?.TryGetValue((string)ID, out identifiers);

            if (identifiers != null && identifiers.Any(x => x && x != this))
            {
                if (!_registered)
                    onReceiverExists?.Invoke();
                _registered = true;
            }
            else if (_registered || identifiers == null || identifiers.Count == 1 && identifiers[0] == this)
            {
                onNoReceiverExists?.Invoke();
                _registered = false;
            }
        }

        private void OnSignalRegistered(string id)
        {
            if (id == (string)ID)
                CheckForReceivers();
        }

        private void OnSignalUnregistered(string id)
        {
            if (id == (string)ID)
                CheckForReceivers();
        }

        private void OnTransmitted(string id)
        {
            if (id == (string)ID)
                OnReceive();
        }

        private void AddSelfToRegistry()
        {
            if (!countAsReceiver) return;
            string id = (string)ID;
            if (string.IsNullOrEmpty(id)) return;
            _registeredNotifications ??= new Dictionary<string, List<ObjectSignal>>();
            if (!_registeredNotifications.TryGetValue(id, out _))
                _registeredNotifications[id] = new List<ObjectSignal>();
            _registeredNotifications[id].Add(this);
        }

        private void RemoveSelfFromRegistry()
        {
            string id = (string)ID;
            if (string.IsNullOrEmpty(id)) return;
            if (_registeredNotifications == null) return;
            if (!_registeredNotifications.TryGetValue(id, out _)) return;
            _registeredNotifications[id].Remove(this);
            if (_registeredNotifications[id].Count == 0)
                _registeredNotifications.Remove(id);
            if (_registeredNotifications.Count == 0)
                _registeredNotifications = null;
            SignalUnregistered?.Invoke(id);
        }
    }
}