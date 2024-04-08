using TriInspectorMVCE;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.VisualScripting
{
    [HideMonoScript]
    public class FindScriptingVariables : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField] private string identifier;
        public UnityEvent<Variables> onFound;
        public UnityEvent onLost;

        public string Identifier
        {
            get => identifier;
            set
            {
                if (identifier == value) return;
                identifier = value;
                if (!string.IsNullOrEmpty(identifier))
                {
                    var vars = ScriptingVariablesIdentifier.Get(identifier);
                    if (vars) onFound?.Invoke(vars);
                    else onLost?.Invoke();
                }
                else
                {
                    onLost?.Invoke();
                }
            }
        }

        private void OnEnable()
        {
            var vars = ScriptingVariablesIdentifier.Get(identifier);
            if (vars) onFound?.Invoke(vars);
            else onLost?.Invoke();
            ScriptingVariablesIdentifier.Registered += OnRegistered;
            ScriptingVariablesIdentifier.Unregistered += OnUnregistered;
        }

        private void OnDisable()
        {
            ScriptingVariablesIdentifier.Registered -= OnRegistered;
            ScriptingVariablesIdentifier.Unregistered -= OnUnregistered;
        }

        private void OnRegistered(ScriptingVariablesIdentifier obj)
        {
            if (obj.Identifier != identifier) return;
            if (obj.TryGetComponent(out Variables variables))
                onFound?.Invoke(variables);
        }

        private void OnUnregistered(ScriptingVariablesIdentifier obj)
        {
            if (obj.Identifier != identifier) return;
            onLost?.Invoke();
        }
    }
}