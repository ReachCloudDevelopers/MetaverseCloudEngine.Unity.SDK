using System.Linq;

using UnityEngine;
using UnityEngine.Events;

using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// Trigger Callbacks for specifically players. Utilizes the <see cref="TriggerCallbacks"/> component as a
    /// helper class.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Trigger Callbacks (Network Player)")]
    public class PlayerTriggerCallbacks : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// Callbacks for trigger detection.
        /// </summary>
        [System.Serializable]
        public class CallbackEvents
        {
            [Tooltip("Invoked when the player enters the trigger.")]
            public UnityEvent<GameObject> onEnter = new();
            [Tooltip("Invoked when capturing the name of the entering player.")]
            public UnityEvent<string> onEnteredPlayerName = new();
            [Tooltip("Invoked when the player exits the trigger.")]
            public UnityEvent<GameObject> onExit = new();
            [Tooltip("Invoked when capturing the name of the exiting player.")]
            public UnityEvent<string> onExitedPlayerName = new();

            /// <summary>
            /// Called when the player enters the trigger.
            /// </summary>
            /// <param name="other">The entering player object.</param>
            public void OnEnter(NetworkObject other)
            {
                onEnter?.Invoke(other.gameObject);
                if (onEnteredPlayerName.GetPersistentEventCount() > 0)
                    other.Networking.GetPlayerName(other.InputAuthorityID, name => onEnteredPlayerName?.Invoke(name));
            }

            /// <summary>
            /// Called when the player exits the trigger.
            /// </summary>
            /// <param name="other">The exiting player.</param>
            public void OnExit(NetworkObject other)
            {
                onExit?.Invoke(other.gameObject);
                if (onExitedPlayerName.GetPersistentEventCount() > 0)
                    other.Networking.GetPlayerName(other.InputAuthorityID, name => onExitedPlayerName?.Invoke(name));
            }
        }

        [Tooltip("Callbacks for both remote and local clients.")]
        [LabelText("Local & Remote")] public CallbackEvents callbacks = new();
        [Tooltip("Callbacks for only the local client enter/exit.")]
        [LabelText("Local")] public CallbackEvents localOnlyCallbacks = new();
        [Tooltip("Callbacks for only the remote client enter/exit.")]
        [LabelText("Remote")] public CallbackEvents remoteOnlyCallbacks = new();

        private void Reset()
        {
            EnsureCallbacksAreConsistent();
        }

        private void Start()
        {
            RegisterCallbacks();
        }

        private void RegisterCallbacks()
        {
            TriggerCallbacks triggerCallbacks = gameObject.AddComponent<TriggerCallbacks>();
            triggerCallbacks.callbacks.onEnter.AddListener(other =>
            {
                if (!this) return;
                if (!isActiveAndEnabled)
                    return;

                NetworkObject netObj = GetPlayerNetObj(other);
                if (!netObj)
                    return;

                callbacks.OnEnter(netObj);
                if (netObj.IsStateAuthority) localOnlyCallbacks.OnEnter(netObj);
                else remoteOnlyCallbacks.OnEnter(netObj);
            });

            triggerCallbacks.callbacks.onExit.AddListener(other =>
            {
                if (!this) return;
                if (!isActiveAndEnabled)
                    return;

                NetworkObject netObj = GetPlayerNetObj(other);
                if (!netObj)
                    return;

                callbacks.OnExit(netObj);
                if (netObj.IsStateAuthority) localOnlyCallbacks.OnExit(netObj);
                else remoteOnlyCallbacks.OnExit(netObj);
            });
        }

        private void EnsureCallbacksAreConsistent()
        {
            if (GetComponentsInParent<Rigidbody>().Any(x => x.transform != transform) && !GetComponent<Rigidbody>())
            {
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }
        }

        private NetworkObject GetPlayerNetObj(GameObject other)
        {
            const string playerTag = "Player";
            return !other.CompareTag(playerTag) ? null : other.GetComponent<NetworkObject>();
        }
    }
}