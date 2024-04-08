#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Labels;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker
{
    public class SetPlayMakerVariable : MonoBehaviour
    {
        public Label variable;
        public bool useGlobals;
        [DisallowNull] public PlayMakerFSM fsm;

        // - Deprecated -
        [SerializeField, HideInInspector] private string variableName;
        // -------

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

        private void Upgrade()
        {
            if (!string.IsNullOrEmpty(variableName))
            {
                variable = variableName;
                variableName = null;
            }
        }

        public void SetIntValue(int value) => SetValue(value);
        public void SetFloatValue(float value) => SetValue(value);
        public void SetStringValue(string value) => SetValue(value);
        public void SetStringValue(LabelReference value) => value.label?.GetValueAsync(v => SetValue(v));
        public void SetBoolValue(bool value) => SetValue(value);
        public void SetObjectValue(Object value) => SetValue(value);

        private void SetValue(object value)
        {
            Upgrade();

            variable.GetValueAsync(vName =>
            {
                if (!this)
                    return;

                if (!TryGetVariable(vName, out var v))
                {
                    MetaverseProgram.Logger.Log("Cannot find variable with name '" + vName + "'");
                    return;
                }

                try { v.RawValue = value; }
                catch { MetaverseProgram.Logger.Log("Failed to set variable '" + vName + "' to value '" + value + "'."); }
            });
        }

        public void SetValueToNull()
        {
            Upgrade();

            variable.GetValueAsync(vName =>
            {
                if (!this)
                    return;

                if (!TryGetVariable(vName, out var v))
                {
                    MetaverseProgram.Logger.Log("Cannot find variable with name '" + vName + "'");
                    return;
                }
                try { v.Clear(); }
                catch { }
            });
        }

        private bool TryGetVariable(string n, out NamedVariable v)
        {
            v = null;
            if (Variables == null || string.IsNullOrEmpty(n))
                return false;
            if (!Variables.Contains(n))
                return false;
            v = Variables.FindVariable(n);
            if (v == null)
                return false;
            return true;
        }

#if UNITY_EDITOR

        [UnityEditor.CanEditMultipleObjects]
        [UnityEditor.CustomEditor(typeof(SetPlayMakerVariable))]
        public class SetPlayMakerVariableEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                var fsmProperty = serializedObject.FindProperty(nameof(SetPlayMakerVariable.fsm));
                var globalsProperty = serializedObject.FindProperty(nameof(SetPlayMakerVariable.useGlobals));
                UnityEditor.EditorGUILayout.PropertyField(globalsProperty);
                if (!globalsProperty.boolValue) UnityEditor.EditorGUILayout.PropertyField(fsmProperty);

                DrawPropertiesExcluding(serializedObject, "m_Script", nameof(SetPlayMakerVariable.fsm), nameof(SetPlayMakerVariable.useGlobals));

                serializedObject.ApplyModifiedProperties();
            }
        }

#endif

    }
}

#endif