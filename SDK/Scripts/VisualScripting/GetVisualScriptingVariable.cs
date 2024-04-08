using System;
using UnityEngine;
using UnityEngine.Events;
using Unity.VisualScripting;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Labels;
using MetaverseCloudEngine.Unity.Scripting.Components;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.VisualScripting
{
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Scripting/Get Scripting Variable")]
    public class GetVisualScriptingVariable : TriInspectorMonoBehaviour
    {
        public Label variable = new();
        
        [Title("Reference")]
        public bool useSceneVariables;

        [HideIf(nameof(UseSceneVariables))]
        [HideIf(nameof(VariablesAssigned))]
        [HideIf(nameof(ScriptAssigned))]
        [DisallowNull]
        public string variablesIdentifier;
        
        [HideIf(nameof(UseSceneVariables))]
        [HideIf(nameof(UseVariablesIdentifier))]
        [HideIf(nameof(ScriptAssigned))]
        [DisallowNull] 
        public Variables variables;
        
        [HideIf(nameof(UseSceneVariables))]
        [HideIf(nameof(UseVariablesIdentifier))]
        [HideIf(nameof(VariablesAssigned))]
        [DisallowNull] 
        public MetaverseScript script;
        
        [Title("Options")]
        public bool getOnStart = true;
        public bool everyFrame;
        
        [Space]
        public GetEvents events = new();

        private object _lastValue;
        private bool _initialValue;
        private VariableDeclarations _cachedDeclarations;

        // - Deprecated -
        [SerializeField, HideInInspector] private string variableName;
        // ------

        /// <summary>
        /// Events called when getting the variable succeeds.
        /// </summary>
        [Serializable]
        public class GetEvents
        {
            public string stringFormat = "{0}";
            public UnityEvent<string> onStringValue;
            public UnityEvent<float> onFloatValue;
            public UnityEvent<int> onIntValue;
            public UnityEvent<bool> onBoolValue;
            public UnityEvent<UnityEngine.Object> onObjectValue;
            public UnityEvent onObjectNull;
        }

        /// <summary>
        /// The variable declarations.
        /// </summary>
        public VariableDeclarations Declarations => _cachedDeclarations ??= 
            useSceneVariables && Variables.ExistInActiveScene 
                ? Variables.ActiveScene 
                : script 
                    ? script.Vars 
                    : variables 
                        ? variables.declarations 
                        : UseVariablesIdentifier 
                            ? ScriptingVariablesIdentifier.Get(variablesIdentifier)?.declarations 
                            : null;

        public string VariablesIdentifier
        {
            get => variablesIdentifier;
            set
            {
                _cachedDeclarations = null;
                variablesIdentifier = value;
            }
        }

        public bool UseVariablesIdentifier => !string.IsNullOrEmpty(variablesIdentifier);

        public bool VariablesAssigned => variables;
        
        public bool ScriptAssigned => script;

        public bool UseSceneVariables
        {
            get => useSceneVariables;
            set
            {
                _cachedDeclarations = null;
                useSceneVariables = value;
            }
        }

        /// <summary>
        /// The name of the variable.
        /// </summary>
        public string VariableName { set => variable?.SetValue(value); }

        private void Awake()
        {
            Upgrade();
        }

        private void OnValidate()
        {
            Upgrade();

            if (variables)
                script = null;
        }

        private void Start()
        {
            if (getOnStart)
                Get(false);
        }

        private void Update()
        {
            if (everyFrame)
                Get(false);
        }

        private void Upgrade()
        {
            if (!string.IsNullOrEmpty(variableName))
            {
                variable = variableName;
                variableName = null;
            }
        }
        
        public void Get()
        {
            Get(true);
        }

        public void Get(bool force)
        {
            Upgrade();

            TryGetVariable(() =>
            {
                var value = Declarations.Get(variable.ToString());
                if (!force && _initialValue && (value == _lastValue || (value is not null && value.Equals(_lastValue))))
                    return;
                
                _lastValue = value;
                _initialValue = true;
                
                if (value is not null)
                {
                    events.onStringValue?.Invoke(!string.IsNullOrEmpty(events.stringFormat) ? string.Format(events.stringFormat, value) : value.ToString());

                    if (value is UnityEngine.Object o)
                        events.onObjectValue?.Invoke(o);

                    if (value is float f)
                    {
                        events.onFloatValue?.Invoke(f);
                        events.onIntValue?.Invoke((int)f);
                    }
                    else if (value is double d)
                    {
                        events.onFloatValue?.Invoke((float)d);
                        events.onIntValue?.Invoke((int)d);
                    }
                    if (value is int i)
                    {
                        events.onFloatValue?.Invoke(i);
                        events.onIntValue?.Invoke(i);
                    }
                    if (value is bool b)
                    {
                        events.onBoolValue?.Invoke(b);
                    }
                }
                else
                {
                    events.onObjectNull?.Invoke();
                }

            });
        }

        private void TryGetVariable(Action onValue, Action onFail = null)
        {
            variable.GetValueAsync(v =>
            {
                if (Declarations == null || !Declarations.IsDefined(v))
                {
                    onFail?.Invoke();
                    return;
                }
                onValue?.Invoke();
            });
        }

    }
}