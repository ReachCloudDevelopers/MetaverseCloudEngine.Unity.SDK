#if PLAYMAKER && METAVERSE_CLOUD_ENGINE
using System;

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Labels;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker
{
    public class GetPlayMakerVariable : MonoBehaviour
    {
        [Serializable]
        public class GetEvents
        {
            public string stringFormat = "{0}";
            public UnityEvent<string> onStringValue;
            public UnityEvent<float> onFloatValue;
            public UnityEvent<int> onIntValue;
            public UnityEvent<bool> onBoolValue;
            public UnityEvent<object> onObjectValue;
            public UnityEvent onObjectNull;
        }

        public Label variable;
        public bool useGlobals;
        [DisallowNull] public PlayMakerFSM fsm;
        public bool getOnStart = true;
        public bool everyFrame;
        public GetEvents events = new();

        // - Deprecated -
        [SerializeField, HideInInspector] private string variableName;
        // -------
        
        private object _lastValue;
        private bool _initialValue;
        private string _lastVariableName;

        public string VariableName {
            set => variable = value;
        }

        public FsmVariables Variables => useGlobals ? FsmVariables.GlobalsComponent ? FsmVariables.GlobalsComponent.Variables : null : fsm ? fsm.FsmVariables : null;

        private void Awake()
        {
            Upgrade();
        }

        private void OnValidate()
        {
            Upgrade();
        }

        private void Reset()
        {
            fsm = GetComponent<PlayMakerFSM>();
        }

        private void Start()
        {
            if (getOnStart)
                Get(false);
        }

        private void FixedUpdate()
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
                var value = Variables.GetVariable(variable.ToString()).RawValue;
                if (!force && _initialValue && (value == _lastValue || (value is not null && value.Equals(_lastValue))))
                    return;
                
                _lastValue = value;
                _initialValue = true;
                
                if (value is not null)
                {
                    events.onStringValue?.Invoke(!string.IsNullOrEmpty(events.stringFormat) ? string.Format(events.stringFormat, value.ToString()) : value.ToString());

                    if (value is UnityEngine.Object o)
                        events.onObjectValue?.Invoke(o);

                    if (value is float f)
                    {
                        events.onFloatValue?.Invoke(f);
                        events.onIntValue?.Invoke((int)f);
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

        private void TryGetVariable(Action onSuccess, Action onFailed = null)
        {
            variable.GetValueAsync(var =>
            {
                if (Variables == null || string.IsNullOrEmpty(var))
                {
                    onFailed?.Invoke();
                    return;
                }
                
                if (!Variables.Contains(var))
                {
                    onFailed?.Invoke();
                    return;
                }
                onSuccess?.Invoke();
            });
        }

#if UNITY_EDITOR

        [UnityEditor.CanEditMultipleObjects]
        [UnityEditor.CustomEditor(typeof(GetPlayMakerVariable), true)]
        public class GetPlayMakerFsmVariableEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                var fsmProperty = serializedObject.FindProperty(nameof(GetPlayMakerVariable.fsm));
                var globalsProperty = serializedObject.FindProperty(nameof(GetPlayMakerVariable.useGlobals));
                UnityEditor.EditorGUILayout.PropertyField(globalsProperty);
                if (!globalsProperty.boolValue) UnityEditor.EditorGUILayout.PropertyField(fsmProperty);

                DrawPropertiesExcluding(serializedObject, "m_Script", nameof(GetPlayMakerVariable.fsm), nameof(GetPlayMakerVariable.useGlobals));

                serializedObject.ApplyModifiedProperties();
            }
        }

#endif
    }
}

#endif