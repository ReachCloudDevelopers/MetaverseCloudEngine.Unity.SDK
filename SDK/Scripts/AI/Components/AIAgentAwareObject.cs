using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class AIAgentAwareObject : TriInspectorMonoBehaviour
    {
        private static readonly Dictionary<string, AIAgentAwareObject> ActionableObjects = new();
        public static readonly List<AIAgentAwareObject> ActiveObjects = new();

        [Serializable]
        public class SupportedObjectAction
        {
            [DisableInPlayMode]
            [Required]
            public string id;
            public bool disabled;
            public UnityEvent<GameObject> onAgentPerformed;
            public UnityEvent onEnabled;
            public UnityEvent onDisabled;
        }

        public bool useGameObjectName = true;
        [Required]
        [HideIf(nameof(useGameObjectName))]
        [SerializeField] private string id;
        [Required]
        public string description;
        public List<SupportedObjectAction> supportedActions = new();

        /// <summary>
        /// The actual action ID.
        /// </summary>
        public string ID => useGameObjectName ? gameObject.name : id;

        private Dictionary<string, SupportedObjectAction> _supportedActionsLookup;  

        private void OnEnable()
        {
            _supportedActionsLookup ??= supportedActions
                .Where(x => !string.IsNullOrWhiteSpace(x.id))
                .ToDictionary(x => x.id, y => y);

            foreach (var action in supportedActions)
            {
                if (action.disabled) action.onDisabled?.Invoke();
                else action.onEnabled?.Invoke();
            }
            
            ActionableObjects[useGameObjectName ? gameObject.name : id] = this;
            ActiveObjects.Add(this);
        }

        private void OnDisable()
        {
            ActionableObjects.Remove(useGameObjectName ? gameObject.name : id);
            ActiveObjects.Remove(this);
        }

        public static AIAgentAwareObject Find(string targetID)
        {
            return ActionableObjects.GetValueOrDefault(targetID);
        }

        public bool IsSupported(string action)
        {
            return _supportedActionsLookup.TryGetValue(action, out var a) && !a.disabled;
        }

        public static IEnumerable<AIAgentAwareObject> FindAll(string actionID)
        {
            return ActionableObjects.Select(x => x.Value).Where(x => x&& x.IsSupported(actionID));
        }

        public void EnableAction(string actionID)
        {
            if (!_supportedActionsLookup.TryGetValue(actionID, out var a)) return;
            if (!a.disabled) return;
            a.disabled = false;
            a.onEnabled?.Invoke();
        }

        public void DisableAction(string actionID)
        {
            if (!_supportedActionsLookup.TryGetValue(actionID, out var a)) return;
            if (a.disabled) return;
            a.disabled = true;
            a.onDisabled?.Invoke();
        }

        public void OnPerformedAction(string actionID, AIAgent aiAgent)
        {
            if (_supportedActionsLookup.TryGetValue(actionID, out var action))
                action.onAgentPerformed?.Invoke(aiAgent.gameObject);
        }
    }
}