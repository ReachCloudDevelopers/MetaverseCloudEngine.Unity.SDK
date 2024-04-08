using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    [Experimental]
    public class States : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class State
        {
            public string name;
            [Attributes.ReadOnly(DuringPlayMode = true)] public bool active;
            public UnityEvent apply;
            public UnityEvent @base;
            public UnityEvent activated;
            public UnityEvent deactivated;

            [NonSerialized]
            public bool wasActivated;
            [NonSerialized]
            public bool initState;

            public bool Active {
                get => active;
                set => active = value;
            }
        }

        public List<State> states = new ();

        private bool _isStarted;

        private void Start()
        {
            Apply();
            _isStarted = true;
        }

        public void ApplyBaseState()
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (!states[i].Active)
                {
                    states[i].@base?.Invoke();
                    if (states[i].wasActivated || !states[i].initState)
                        states[i].deactivated?.Invoke();
                    states[i].wasActivated = false;
                    states[i].initState = true;
                }
            }
        }

        public void ToggleState(string state)
        {
            State s = states.FirstOrDefault(x => x.name == state);
            if (s != null)
            {
                s.Active = !s.Active;
                Apply();
            }
        }

        public void ActivateState(string state)
        {
            State s = states.FirstOrDefault(x => x.name == state);
            if (s != null)
            {
                s.Active = true;
                Apply();
            }
        }

        public void DeactivateState(string state)
        {
            State s = states.FirstOrDefault(x => x.name == state);
            if (s != null)
            {
                s.Active = false;
                Apply();
            }
        }

        public void Apply()
        {
            if (!_isStarted || !isActiveAndEnabled)
                return;
            ApplyBaseState();
            ApplyActiveStates();
        }

        private void ApplyActiveStates()
        {
            for (int i = states.Count - 1; i >= 0; i--)
            {
                if (states[i].Active)
                {
                    states[i].apply?.Invoke();
                    if (!states[i].wasActivated || !states[i].initState)
                        states[i].activated?.Invoke();
                    states[i].wasActivated = true;
                    states[i].initState = true;
                }
            }
        }
    }
}
