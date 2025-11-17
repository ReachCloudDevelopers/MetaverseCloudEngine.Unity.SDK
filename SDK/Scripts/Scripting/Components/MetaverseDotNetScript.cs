#if MV_DOTNOW_SCRIPTING

using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Component that executes C# scripts via DotNow using a compiled assembly and class name.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Scripting/Metaverse .NET Script")]
    public class MetaverseDotNetScript : MetaSpaceBehaviour
    {
        [Serializable]
        public enum SerializedFieldKind
        {
            Bool,
            Int,
            Float,
            String,
            Enum,
            Vector2,
            Vector3,
            Color,
            ObjectReference
        }

        [Serializable]
        public class SerializedField
        {
            public string fieldName;
            public string declaringTypeName;
            public SerializedFieldKind kind;
            public bool hasOverride;
            public bool boolValue;
            public int intValue;
            public float floatValue;
            public string stringValue;
            public UnityEngine.Object objectReference;
            public Vector2 vector2Value;
            public Vector3 vector3Value;
            public Color colorValue;
        }

        [SerializeField]
        [Tooltip("Compiled C# assembly (.dll.bytes) that contains the script class implementing IMetaverseDotNetScript.")]
        private TextAsset assemblyAsset;

        [SerializeField]
        [Tooltip("Fully qualified name of the class inside the assembly that implements IMetaverseDotNetScript.")]
        private string className;

        [SerializeField]
        [Tooltip("Serialized field values for the selected script class.")]
        private List<SerializedField> serializedFields = new List<SerializedField>();

        private IMetaverseDotNetScript _scriptInstance;
        private bool _initialized;

        public TextAsset AssemblyAsset
        {
            get => assemblyAsset;
            set => assemblyAsset = value;
        }

        public string ClassName
        {
            get => className;
            set => className = value;
        }

        public List<SerializedField> SerializedFields => serializedFields;

        protected override void Awake()
        {
            base.Awake();

            if (TryEnsureScriptInstance())
                SafeInvoke(s => s.OnAwake());
        }


        private void Start()
        {
            if (TryEnsureScriptInstance())
                SafeInvoke(s => s.OnStart());
        }

        protected virtual void OnEnable()
        {
            if (TryEnsureScriptInstance())
                SafeInvoke(s => s.OnOnEnable());
        }

        protected virtual void OnDisable()
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnDisable());
        }

        private bool TryEnsureScriptInstance()
        {
            if (_scriptInstance != null && _initialized)
                return true;

            if (!isActiveAndEnabled)
                return false;

            if (!assemblyAsset)
            {
                MetaverseProgram.Logger.LogWarning("[METAVERSE_DOTNET_SCRIPT] No assembly assigned.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                MetaverseProgram.Logger.LogWarning($"[METAVERSE_DOTNET_SCRIPT] No class name specified on '{name}'.");
                return false;
            }

            if (MetaverseDotNetScriptCache.Current == null)
            {
                MetaverseProgram.Logger.LogError("[METAVERSE_DOTNET_SCRIPT] No MetaverseDotNetScriptCache available in the scene.");
                return false;
            }

            try
            {
                if (!MetaverseDotNetScriptCache.Current.TryCreateScriptInstance(assemblyAsset, className, out var instance))
                    return false;

                _scriptInstance = instance;

                ApplySerializedFieldsToInstanceIfAvailable();

                if (!_initialized)
                {
                    var context = new MetaverseDotNetScriptContext(this, MetaSpace);
                    SafeInvoke(s => s.OnInitialize(context));
                    _initialized = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Failed to create script instance '{className}' from '{assemblyAsset?.name}': {ex}");
                _scriptInstance = null;
                return false;
            }
        }

        public void ApplySerializedFieldsToInstanceIfAvailable()
        {
            if (_scriptInstance == null)
                return;

            var cache = MetaverseDotNetScriptCache.Current;
            cache?.ApplySerializedFields(this, _scriptInstance);
        }

        private void Update()
        {
            if (TryEnsureScriptInstance())
                SafeInvoke(s => s.OnUpdate());
        }

        private void LateUpdate()
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnLateUpdate());
        }

        private void FixedUpdate()
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnFixedUpdate());
        }

        private void OnGUI()
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnGUI());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnTriggerEnter(other));
        }

        private void OnTriggerExit(Collider other)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnTriggerExit(other));
        }

        private void OnTriggerStay(Collider other)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnTriggerStay(other));
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnTriggerEnter2D(collision));
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnTriggerExit2D(collision));
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnTriggerStay2D(collision));
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnAnimatorIK(layerIndex));
        }

        private void OnAnimatorMove()
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnAnimatorMove());
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnCollisionEnter(collision));
        }

        private void OnCollisionExit(Collision collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnCollisionExit(collision));
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnCollisionStay(collision));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnCollisionEnter2D(collision));
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnCollisionExit2D(collision));
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnOnCollisionStay2D(collision));
        }


        protected override void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();
            if (TryEnsureScriptInstance())
                SafeInvoke(s => s.OnMetaSpaceBehaviourInitialize());
        }

        protected override void OnMetaSpaceServicesRegistered()
        {
            base.OnMetaSpaceServicesRegistered();
            if (TryEnsureScriptInstance())
                SafeInvoke(s => s.OnMetaSpaceServicesRegistered());
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            if (_scriptInstance != null)
                SafeInvoke(s => s.OnMetaSpaceBehaviourDestroyed());
            base.OnMetaSpaceBehaviourDestroyed();
        }

        protected override void OnDestroy()
        {
            if (!MetaverseProgram.IsQuitting)
            {
                base.OnDestroy();
                SafeInvoke(s => s.OnOnDestroy());
            }
            else
            {
                base.OnDestroy();
            }

            if (_scriptInstance != null && MetaverseDotNetScriptCache.Current != null)
            {
                MetaverseDotNetScriptCache.Current.NotifyScriptInstanceDestroyed(_scriptInstance);
            }

            _scriptInstance = null;
        }

        private void SafeInvoke(Action<IMetaverseDotNetScript> action)
        {
            if (_scriptInstance == null)
                return;

            try
            {
                action(_scriptInstance);
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Unhandled exception while invoking script '{className}': {ex}");
             }
        }
    }
}


#endif // MV_DOTNOW_SCRIPTING
