using System;
using System.Collections.Generic;
using TriInspectorMVCE;
using Unity.VisualScripting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.VisualScripting
{
    [HideMonoScript]
    [RequireComponent(typeof(Variables))]
    public class ScriptingVariablesIdentifier : TriInspectorMonoBehaviour
    {
        private static readonly Dictionary<string, Variables> Identifiers = new();
        
        [Tooltip("The identifier of the variables. This is used to retrieve the variables reference.")]
        [Required]
        [SerializeField] private string identifier;

        public static event Action<ScriptingVariablesIdentifier> Registered;
        public static event Action<ScriptingVariablesIdentifier> Unregistered;

        public string Identifier
        {
            get => identifier;
            set
            {
                if (identifier == value)
                    return;

                if (!string.IsNullOrEmpty(identifier))
                {
                    Identifiers.Remove(identifier);
                    Unregistered?.Invoke(this);   
                }
                
                identifier = value;
                if (!string.IsNullOrEmpty(identifier))
                {
                    Identifiers[identifier] = GetComponent<Variables>();
                    Registered?.Invoke(this);
                }
            }
        }

        private void Awake()
        {
            if (!string.IsNullOrEmpty(identifier))
            {
                Identifiers[identifier] = GetComponent<Variables>();
                Registered?.Invoke(this);
            }
        }

        private void OnDestroy()
        {
            if (Identifiers.Remove(identifier))
                Unregistered?.Invoke(this);
        }

        public static Variables Get(string identifier)
        {
            return !Identifiers.TryGetValue(identifier, out var variables) ? null : variables;
        }
    }
}