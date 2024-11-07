using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    public class AIAgentAwareObject : TriInspectorMonoBehaviour
    {
        public static readonly List<AIAgentAwareObject> ActiveObjects = new();

        [Serializable]
        public class SupportedObjectAction
        {
            [DisableInPlayMode]
            [Required]
            public string id;
            [InfoBox("Call EnableAction or DisableAction to enable or disable this action.")]
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

        private void OnEnable()
        {
            foreach (var action in supportedActions)
            {
                if (action.disabled) action.onDisabled?.Invoke();
                else action.onEnabled?.Invoke();
            }
            ActiveObjects.Add(this);
        }

        private void OnDisable()
        {
            ActiveObjects.Remove(this);
        }

        public static AIAgentAwareObject Find(string targetID)
        {
            return ActiveObjects.FirstOrDefault(x => x.ID == targetID);
        }

        public bool IsActionSupported(string action)
        {
            return supportedActions.Any(x => x.id == action && !x.disabled);
        }

        public static IEnumerable<AIAgentAwareObject> FindAll(string actionID)
        {
            return ActiveObjects.Where(x => x&& x.IsActionSupported(actionID));
        }

        public void EnableAction(string actionID)
        {
            if (!TryGetAction(actionID, out var a)) return;
            if (!a.disabled) return;
            a.disabled = false;
            if (!isActiveAndEnabled) return;
            a.onEnabled?.Invoke();
        }

        public void DisableAction(string actionID)
        {
            if (!TryGetAction(actionID, out var a)) return;
            if (a.disabled) return;
            a.disabled = true;
            if (!isActiveAndEnabled) return;
            a.onDisabled?.Invoke();
        }

        public void OnPerformedAction(string actionID, AIAgent aiAgent)
        {
            if (!TryGetAction(actionID, out var action)) return;
            action.onAgentPerformed?.Invoke(aiAgent.gameObject);
        }
        
        private bool TryGetAction(string actionID, out SupportedObjectAction action)
        {
            action = GetAction(actionID);
            return action != null;
        }
        
        private SupportedObjectAction GetAction(string actionID)
        {
            return supportedActions.FirstOrDefault(x => x.id == actionID);
        }
    }
}