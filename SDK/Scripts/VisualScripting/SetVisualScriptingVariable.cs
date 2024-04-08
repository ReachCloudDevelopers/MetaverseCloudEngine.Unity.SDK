using System;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Labels;
using MetaverseCloudEngine.Unity.Scripting.Components;
using TriInspectorMVCE;
using Unity.VisualScripting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.VisualScripting
{
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Scripting/Set Scripting Variable")]
    public class SetVisualScriptingVariable : TriInspectorMonoBehaviour
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

        // - Deprecated -
        [SerializeField, HideInInspector] private string variableName;
        // ------
        
        private VariableDeclarations _cachedDeclarations;

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

        public bool UseVariablesIdentifier => !string.IsNullOrEmpty(variablesIdentifier);
        
        public string VariablesIdentifier
        {
            get => variablesIdentifier;
            set
            {
                _cachedDeclarations = null;
                variablesIdentifier = value;
            }
        }

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

        private void Awake() => Validate();

        private void OnValidate() => Validate();

        private void Reset() => variables = GetComponent<Variables>();

        private void Validate()
        {
            if (!string.IsNullOrEmpty(variableName))
            {
                variable = variableName;
                variableName = null;
            }

            if (variables) script = null;
            if (script) variables = null;
            if (useSceneVariables)
            {
                script = null;
                variables = null;
            }
        }

        public void SetIntValue(int value) => SetValue(value);
        public void SetFloatValue(float value) => SetValue(value);
        public void SetStringValue(string value) => SetValue(value);
        public void SetStringValue(LabelReference value) => value.label?.GetValueAsync(str => SetValue(str));
        public void SetBoolValue(bool value) => SetValue(value);
        public void SetObjectValue(UnityEngine.Object value) => SetValue(value);

        private void SetValue(object value)
        {
            Validate();

            if (Declarations == null)
                return;

            variable.GetValueAsync(v =>
            {
                if (!Declarations.IsDefined(v)) return;
                try { Declarations.Set(v, value); }
                catch (Exception) { /* ignored */ }
            }); 
        }
    }
}