using Jint;
using Jint.Native;
using TMPro;
using Cinemachine;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.VisualScripting;
using Unity.Collections;
#if MV_UNITY_AI_NAV
using Unity.AI.Navigation;
#endif

#if MV_XRCOREUTILS
using XROrigin = Unity.XR.CoreUtils.XROrigin;
#endif

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TriInspectorMVCE;

#if MV_UNITY_AI_INFERENCE
using Unity.InferenceEngine;
#endif

// ReSharper disable RedundantUnsafeContext

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// A component that's used to execute JavaScript with Unity-style functions.
    /// By default, Unity only supports C# scripts, but this component allows you to write scripts in javascript
    /// using the Jint library.
    /// </summary>
    [HideMonoScript]
    [HelpURL(
        "https://reach-cloud.gitbook.io/reach-explorer-documentation/docs/development-guide/unity-engine-sdk/custom-scripting/custom-javascript")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Scripting/Metaverse Script")]
    [ExecuteAlways]
    public class MetaverseScript : NetworkObjectBehaviour
    {
        /// <summary>
        /// An object wrapper that allows you to access the console from javascript.
        /// </summary>
        [UsedImplicitly]
        public class ConsoleObject
        {
            private readonly string _prefix;

            public ConsoleObject(string prefix) => _prefix = prefix;

            // ReSharper disable once InconsistentNaming
            public void log(object o) => MetaverseProgram.Logger.Log($"[{_prefix}] {o}");

            // ReSharper disable once InconsistentNaming
            public void error(object o) => MetaverseProgram.Logger.LogError($"[{_prefix}] {o}");

            // ReSharper disable once InconsistentNaming
            public void warn(object o) => MetaverseProgram.Logger.LogWarning($"[{_prefix}] {o}");

            // ReSharper disable once InconsistentNaming
            public void info(object o) => MetaverseProgram.Logger.Log($"[{_prefix}] {o}");
        }

#pragma warning disable CS0618
        private readonly static List<string> BlackListedNames = new()
        {
            nameof(Application.OpenURL),
            nameof(SendMessage),
            nameof(SendMessageUpwards),
            nameof(BroadcastMessage),
#if UNITY_2022_2_OR_NEWER
            nameof(FindAnyObjectByType),
            nameof(FindObjectsByType),
#endif
            nameof(FindSceneObjectsOfType),
            nameof(FindObjectOfType),
            nameof(FindObjectsOfType),
            nameof(FindObjectsOfTypeAll),
            nameof(FindObjectsOfTypeIncludingAssets),
            nameof(DontDestroyOnLoad)
        };
#pragma warning restore CS0618

        private readonly static List<string> BlackListedNamespaces = new()
        {
            "System.IO",
            "System.Reflection",
            "System.Web",
            "System.Http",
            "System.CodeDom",
            "Microsoft.Win32",
            "Microsoft.SafeHandles",
        };

        private readonly static List<string> BlackListedTypes = new()
        {
            nameof(Resources),
            nameof(AssetBundle),
            nameof(PlayerPrefs),
        };

        [Flags]
        public enum GlobalTypeImports
        {
            None = 0,
            [InspectorName("Oculus.VR")]
            MetaQuest = 1,
        }

        /// <summary>
        /// The supported <see cref="MetaverseScript"/> functions.
        /// </summary>
        [Flags]
        public enum ScriptFunctions
        {
            Awake = 1,
            OnEnable = 2,
            OnDisable = 4,
            Start = 8,
            Update = 16,
            LateUpdate = 32,
            FixedUpdate = 64,
            OnDestroy = 128,

            OnTriggerEnter = 256,
            OnTriggerExit = 512,
            OnTriggerStay = 1024,
            OnTriggerEnter2D = 2048,
            OnTriggerExit2D = 4096,
            OnTriggerStay2D = 8192,

            OnAnimatorIK = 16384,
            OnAnimatorMove = 32768,

            OnCollisionEnter = 65536,
            OnCollisionExit = 131072,
            OnCollisionStay = 262144,
            OnCollisionEnter2D = 524288,
            OnCollisionExit2D = 1048576,
            OnCollisionStay2D = 2097152,

            OnMetaSpaceBehaviourInitialize = 4194304,
            OnMetaSpaceBehaviourDestroyed = 8388608,
            OnMetaSpaceServicesRegistered = 16777216,

            OnNetworkReady = 33554432,
            RegisterNetworkRPCs = 67108864,
            UnRegisterNetworkRPCs = 134217728,

            OnGUI = 268435456,
        }

        private const string ThisProperty = "_this";
        private const string GameObjectProperty = "gameObject";
        private const string TransformProperty = "transform";
        private const string ConsoleObjectProperty = "console";
        private const string IsUnityNullFunctionOld1 = "isUnityNull";
        private const string IsUnityNullFunctionOld2 = "NULL";
        private const string CoroutineFunction = "StartCoroutine";
        private const string GetMetaverseScriptFunction = "GetMetaverseScript";
        private const string PrintFunction = "print";
        private const string NewGuidFunction = "NewGuid";
        private const string GetGlobalFunction = "GetStaticReference";
        private const string SetGlobalFunction = "SetStaticReference";
        private const string MetaSpaceProperty = "MetaSpace";
        private const string GetNetworkObjectFunction = "GetNetworkObject";
        private const string IsInputAuthorityFunction = "GetIsInputAuthority";
        private const string IsStateAuthorityFunction = "GetIsStateAuthority";
        private const string GetEnabledFunction = "GetEnabled";
        private const string SetEnabledFunction = "SetEnabled";
        private const string SetTimeoutFunction = "setTimeout";
        private const string ClearTimeoutFunction = "clearTimeout";
        private const string RegisterRPCFunction = "RegisterRPC";
        private const string UnregisterRPCFunction = "UnregisterRPC";
        private const string ServerRPCFunction = "ServerRPC";
        private const string ServerRPCBufferedFunction = "ServerRPCBuffered";
        private const string ClientRPCFunction = "ClientRPC";
        private const string ClientRPCBufferedFunction = "ClientRPCBuffered";
        private const string ClientRPCOthersFunction = "ClientRPCOthers";
        private const string ClientRPCOthersBufferedFunction = "ClientRPCOthersBuffered";
        private const string PlayerRPCFunction = "PlayerRPC";
        private const string GetHostIDFunction = "GetHostID";
        private const string SpawnNetworkPrefabFunction = "SpawnNetworkPrefab";
        private const string AwaitFunction = "await";
        private const string AlertFunction = "alert";
        private const string ShowDialogFunction = "ShowDialog";
        private const string ShowForcedDialogFunction = "ShowForcedDialog";
        private const string ShowDialogComplexFunction = "ShowDialogComplex";
        private const string ShowForcedDialogComplexFunction = "ShowForcedDialogComplex";

        [Tooltip("The file that contains the javascript.")]
        [Required] public TextAsset javascriptFile;
        [SerializeField] private GlobalTypeImports globalTypeImports = GlobalTypeImports.None;
        [SerializeField] private TextAsset[] includes = Array.Empty<TextAsset>();
        [SerializeField] private Variables variables;

        private bool _ready;
        private Engine _engine;
        private Dictionary<ScriptFunctions, JsValue> _methods;
        private ConsoleObject _console;
        private readonly Dictionary<string, JsValue> _functionLookup = new();
        private static int _timeoutHandleIndex;
        private readonly HashSet<int> _timeoutHandles = new();
        private readonly Queue<Action> _initializationMethodQueue = new();

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Retrieves the console object allowed to be accessed from JavaScript.
        /// </summary>
        private ConsoleObject console => _console ??= new ConsoleObject(javascriptFile ? javascriptFile.name : "Missing Script");

        /// <summary>
        /// Gets the variable declarations for the JavaScript file.
        /// </summary>
        public VariableDeclarations Vars => variables ? variables.declarations : null;

        protected override unsafe void OnDestroy()
        {
            _initializationMethodQueue.Clear(); // Make sure no initialization methods are triggered.

            base.OnDestroy();

            if (_methods != null && _ready)
            {
                JsValue method = null;
                if (_methods?.TryGetValue(ScriptFunctions.OnDestroy, out method) == true)
                    _ = _engine.Invoke(method);
            }

            _engine?.Dispose();
        }

        private unsafe void OnEnable()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnEnable, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void OnDisable()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnDisable, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void Start()
        {
            try
            {
                if (!Application.isPlaying)
                {
                    return;
                }
                if (MetaSpace.Instance)
                {
                    MetaSpace.OnReady(OnMetaSpaceReady);
                    return;
                }
                OnMetaSpaceReady();
            }
            catch (Exception e)
            {
                OnFailed(e.GetBaseException());
            }
            return;

            void OnFailed(object e)
            {
                console.error(
                    $"Failed to initialize MetaverseScript '{(javascriptFile ? javascriptFile.name : "Missing Script")}': {e}");
                if (this) enabled = false;
            }

            void OnMetaSpaceReady()
            {
                if (!this) return;
                MetaverseDispatcher.AtEndOfFrame(() =>
                {
                    if (!this) return;
                    try
                    {
                        TryInitializeEngine(() =>
                        {
                            if (this && gameObject.activeInHierarchy)
                                CallAwake();
                            else
                                MetaverseDispatcher.WaitUntil(() => !this || gameObject.activeInHierarchy, () =>
                                {
                                    if (this) CallAwake();
                                });

                            if (this && isActiveAndEnabled)
                                CallOnEnabled();
                            else
                            {
                                MetaverseDispatcher.WaitUntil(() => !this || isActiveAndEnabled, () =>
                                {
                                    if (this) CallOnEnabled();
                                });
                            }
                            return;

                            unsafe void CallOnEnabled()
                            {
                                if (!this) return;
                                JsValue onEnableMethod = null;
                                if (_methods?.TryGetValue(ScriptFunctions.OnEnable, out onEnableMethod) == true)
                                    _ = _engine.Invoke(onEnableMethod);
                                JsValue startMethod = null;
                                if (enabled && _methods?.TryGetValue(ScriptFunctions.Start, out startMethod) == true)
                                    _ = _engine.Invoke(startMethod);
                            }

                            unsafe void CallAwake()
                            {
                                if (!this) return;
                                _ready = true;

                                JsValue awakeMethod = null;
                                if (_methods?.TryGetValue(ScriptFunctions.Awake, out awakeMethod) == true)
                                    _ = _engine.Invoke(awakeMethod);

                                while (_initializationMethodQueue.TryDequeue(out var a))
                                {
                                    try
                                    {
                                        a?.Invoke();
                                    }
                                    catch (Exception e)
                                    {
                                        OnFailed(e.GetBaseException());
                                    }
                                }
                            }
                        }, e =>
                        {
                            OnFailed($"Failed to initialize the engine: {e}");
                        });
                    }
                    catch (Exception e)
                    {
                        OnFailed(e.GetBaseException());
                    }
                });
            }
        }

        private void OnGUI()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnGUI, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void Update()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.Update, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void LateUpdate()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.LateUpdate, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void FixedUpdate()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.FixedUpdate, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void OnTriggerEnter(Collider other)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerEnter, out method) == true)
                _ = _engine.Invoke(method, other);
        }

        private unsafe void OnTriggerExit(Collider other)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerExit, out method) == true)
                _ = _engine.Invoke(method, other);
        }

        private unsafe void OnTriggerStay(Collider other)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerStay, out method) == true)
                _ = _engine.Invoke(method, other);
        }

        private unsafe void OnTriggerEnter2D(Collider2D collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerEnter2D, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnTriggerExit2D(Collider2D collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerExit2D, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnTriggerStay2D(Collider2D collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerStay2D, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnAnimatorIK(int layer)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnAnimatorIK, out method) == true)
                _ = _engine.Invoke(method, layer);
        }

        private unsafe void OnAnimatorMove()
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnAnimatorMove, out method) == true)
                _ = _engine.Invoke(method);
        }

        private unsafe void OnCollisionEnter(Collision collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionEnter, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnCollisionExit(Collision collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionExit, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnCollisionStay(Collision collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionStay, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionEnter2D, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnCollisionExit2D(Collision2D collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionExit2D, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private unsafe void OnCollisionStay2D(Collision2D collision)
        {
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionStay2D, out method) == true)
                _ = _engine.Invoke(method, collision);
        }

        public override unsafe void OnNetworkReady(bool offline)
        {
            base.OnNetworkReady(offline);
            OnEngineReady(() =>
            {
                JsValue method = null;
                if (_methods?.TryGetValue(ScriptFunctions.OnNetworkReady, out method) == true)
                    _ = _engine.Invoke(method, offline);
            });
        }

        protected override unsafe void RegisterNetworkRPCs()
        {
            base.RegisterNetworkRPCs();
            OnEngineReady(() =>
            {
                JsValue method = null;
                if (_methods?.TryGetValue(ScriptFunctions.RegisterNetworkRPCs, out method) == true)
                    _ = _engine.Invoke(method);
            });
        }

        protected override unsafe void UnRegisterNetworkRPCs()
        {
            base.UnRegisterNetworkRPCs();
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.UnRegisterNetworkRPCs, out method) == true)
                _ = _engine.Invoke(method);
        }

        protected override unsafe void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();
            OnEngineReady(() =>
            {
                JsValue method = null;
                if (_methods?.TryGetValue(ScriptFunctions.OnMetaSpaceBehaviourInitialize, out method) == true)
                    _ = _engine.Invoke(method);
            });
        }

        protected override unsafe void OnMetaSpaceServicesRegistered()
        {
            base.OnMetaSpaceServicesRegistered();
            OnEngineReady(() =>
            {
                JsValue method = null;
                if (_methods?.TryGetValue(ScriptFunctions.OnMetaSpaceServicesRegistered, out method) == true)
                    _ = _engine.Invoke(method);
            });
        }

        protected override unsafe void OnMetaSpaceBehaviourDestroyed()
        {
            base.OnMetaSpaceBehaviourDestroyed();
            if (!_ready) return;
            JsValue method = null;
            if (_methods?.TryGetValue(ScriptFunctions.OnMetaSpaceBehaviourDestroyed, out method) == true)
                _ = _engine.Invoke(method);
        }

        /// <summary>
        /// Executes a javascript function.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        [Obsolete("Please use ExecuteVoid instead.")]
        public void ExecuteFunction(string fn) => ExecuteVoid(fn);

        /// <summary>
        /// Executes a javascript function.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        public unsafe void ExecuteVoid(string fn)
        {
            if (!this)
                return;

            if (!isActiveAndEnabled)
                return;

            if (string.IsNullOrEmpty(fn))
                return;

            if (!_ready)
            {
                console.warn(
                    $"The script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
                return;
            }

            if (!_functionLookup.TryGetValue(fn, out var method))
                _functionLookup[fn] = method = _engine.GetValue(fn);

            if (method != null && !method.IsUndefined())
                _ = _engine.Invoke(method);
        }

        /// <summary>
        /// Executes a javascript function with arguments.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        /// <param name="args">The arguments to pass to the function.</param>
        public unsafe void ExecuteVoid(string fn, object[] args)
        {
            if (!this)
                return;

            if (!isActiveAndEnabled)
                return;

            if (string.IsNullOrEmpty(fn))
                return;

            if (!_ready)
            {
                console.log(
                    $"Script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
                return;
            }

            if (!_functionLookup.TryGetValue(fn, out var method))
                _functionLookup[fn] = method = _engine.GetValue(fn);

            if (method != null && !method.IsUndefined())
                _ = _engine.Invoke(method, args);
        }

        /// <summary>
        /// Executes a JavaScript function with arguments.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        /// <param name="arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function.</returns>
        public unsafe JsValue Execute(string fn, object[] arguments)
        {
            if (!this)
                return null;

            if (!isActiveAndEnabled)
                return null;

            if (string.IsNullOrEmpty(fn))
                return null;

            if (!_ready)
            {
                console.warn(
                    $"Script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
                return null;
            }

            if (!_functionLookup.TryGetValue(fn, out var method))
                _functionLookup[fn] = method = _engine.GetValue(fn);

            if (method != null && !method.IsUndefined())
                return _engine.Invoke(method, arguments);

            return null;
        }

        /// <summary>
        /// Executes a javascript function without any arguments.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        public unsafe JsValue Execute(string fn)
        {
            if (!this)
                return null;

            if (!isActiveAndEnabled)
                return null;

            if (string.IsNullOrEmpty(fn))
                return null;

            if (!_ready)
            {
                console.log(
                    $"Script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
                return null;
            }

            if (!_functionLookup.TryGetValue(fn, out var method))
                _functionLookup[fn] = method = _engine.GetValue(fn);

            if (method != null && !method.IsUndefined())
                return _engine.Invoke(method);

            return null;
        }

        /// <summary>
        /// Gets a Unity variable with the given name.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <returns>The variable value.</returns>
        public unsafe object GetVar(string variableName)
        {
            return TryGetVar(variableName, null);
        }

        /// <summary>
        /// Gets a Unity variable with the given name, or a default value if it doesn't exist.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="defaultValue">The default value to return if the variable doesn't exist.</param>
        /// <returns>The variable value.</returns>
        public unsafe object TryGetVar(string variableName, object defaultValue)
        {
            if (!variables) return defaultValue;
            return variables.declarations?.IsDefined(variableName) == true
                ? variables.declarations.Get(variableName)
                : defaultValue;
        }

        /// <summary>
        /// Sets a Unity variable with the given name.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="value">The value to set it to.</param>
        public unsafe void SetVar(string variableName, object value)
        {
            if (!variables) return;
            variables.declarations?.Set(variableName, value);
        }

        /// <summary>
        /// Tries to set a Unity variable with the given name.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="value">The value to set it to.</param>
        /// <returns>true if the variable was set, false otherwise.</returns>
        public unsafe bool TrySetVar(string variableName, object value)
        {
            if (variables == null) return false;
            if (variables.declarations?.IsDefined(variableName) != true) return false;
            variables.declarations.Set(variableName, value);
            return true;
        }

        /// <summary>
        /// Gets a property with the given name from the javascript engine.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The property value.</returns>
        public unsafe JsValue GetProperty(string propertyName)
        {
            return GetProperty(propertyName, JsValue.Undefined);
        }

        /// <summary>
        /// Gets a property with the given name from the javascript engine.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="defaultValue">The default value to return if the property doesn't exist.</param>
        /// <returns>The property value.</returns>
        public unsafe JsValue GetProperty(string propertyName, JsValue defaultValue)
        {
            var v = _engine?.GetValue(propertyName);
            if (v == null || v.IsUndefined())
                return defaultValue;
            return v;
        }

        /// <summary>
        /// Sets a property with the given name from the javascript engine.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set it to.</param>
        /// <returns></returns>
        public unsafe bool SetProperty(string propertyName, object value)
        {
            if (_engine == null)
                return false;
            _ = _engine.SetValue(propertyName, value);
            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the given member is allowed to be accessed from javascript.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <returns>true if the member is allowed, false otherwise.</returns>
        public static unsafe bool FilterAllowedMembers(MemberInfo member)
        {
            if (member is null ||
                string.IsNullOrEmpty(member.Name) ||
                string.IsNullOrEmpty(member.DeclaringType?.FullName))
                return false;
            return
                !IsBlackListedMemberName(member.Name) &&
                (member.DeclaringType == null || !IsBlackListedMemberName(member.DeclaringType.Name, true)) &&
                !IsInBlackListedNamespace(member);
        }

        /// <summary>
        /// Gets a value indicating whether the given type is allowed to be accessed from javascript.
        /// </summary>
        /// <param name="member">The type to check.</param>
        /// <returns>true if the type is blacklisted, false otherwise.</returns>
        public static unsafe bool IsInBlackListedNamespace(MemberInfo member)
        {
            if (member.DeclaringType == null || string.IsNullOrEmpty(member.DeclaringType.Namespace))
            {
                return false;
            }

            return BlackListedNamespaces.Any(x => x.StartsWith(member.DeclaringType.Namespace));
        }

        /// <summary>
        /// Gets a value indicating whether the given member name is blacklisted.
        /// </summary>
        /// <param name="value">The member name to check.</param>
        /// <param name="isType">true if the member name is a type name, false otherwise.</param>
        /// <returns>true if the member name is blacklisted, false otherwise.</returns>
        public static unsafe bool IsBlackListedMemberName(string value, bool isType = false)
        {
            return BlackListedNames.Contains(value) || (isType && BlackListedTypes.Contains(value));
        }

        private unsafe Func<object> DefineVar(string variableName, object defaultValue)
        {
            return () => TryGetVar(variableName, defaultValue);
        }

        private unsafe Func<object> DefineTypedVar(string variableName, string typePath, object defaultValue)
        {
            return () =>
            {
                var output = TryGetVar(variableName, defaultValue);
                if (output is UnityEngine.Object o && !o)
                    return null;
                return output;
            };
        }

        private unsafe void OnEngineReady(Action a)
        {
            if (_ready)
            {
                a?.Invoke();
                return;
            }

            _initializationMethodQueue.Enqueue(a);
        }

        private void TryInitializeEngine(Action onFinished, Action<object> onFailed)
        {
            if (!javascriptFile)
                return;

            _engine = new Engine(o => DefaultEngineOptions(o, true))
                .Do(e => GetEmbeddedGlobalMembers(e, this)
                    .ForEach(m =>
                    {
                        if (m.Value is Delegate d)
                        {
                            e.SetValue(m.Key, d);
                            return;
                        }
                        e.SetValue(m.Key, m.Value);
                    }));

            UniTask.Void(async c =>
            {
                foreach (var include in includes)
                    if (include && !string.IsNullOrEmpty(include.text))
                        _engine.Execute(await MetaverseScriptCache.Current.GetScriptAsync(include, c));
                _engine.Execute(await MetaverseScriptCache.Current.GetScriptAsync(javascriptFile, c));

                var methods = (ScriptFunctions[])Enum.GetValues(typeof(ScriptFunctions));
                foreach (var method in methods)
                    CacheMethod(method);

                if (_methods != null)
                    onFinished?.Invoke();
                else
                    onFailed?.Invoke("Failed to initialize the engine.");

            }, cancellationToken: destroyCancellationToken);

        }

        private unsafe void DefaultEngineOptions(Options options, bool strict)
        {
            options.AllowClr(GetAssemblies())
                .AllowClrWrite()
                .AllowOperatorOverloading()
                //.SetTypeResolver(new Jint.Runtime.Interop.TypeResolver { MemberFilter = FilterAllowedMembers })
                .AddExtensionMethods(GetExtensionMethodTypes())
                .CatchClrExceptions(OnJavaScriptCLRException);

            if (strict)
                options.Strict();

            options.Interop.TrackObjectWrapperIdentity = false;
        }

        private unsafe bool OnJavaScriptCLRException(Exception exception)
        {
            console.error(
                $"An exception occurred in a javascript script '{(javascriptFile ? javascriptFile.name : "Missing Script")}': {exception.GetBaseException()}");
            return true;
        }

        private static IEnumerator CoroutineUpdate(Func<object> foo)
        {
            object val;

            Next();
            while (val is not bool b || b)
            {
                object retVal = val switch
                {
                    int i => new WaitForSeconds(i),
                    double d => new WaitForSeconds((float)d),
                    float f => new WaitForSeconds(f),
                    Func<bool> m => new WaitUntil(m),
                    _ => null
                };

                yield return retVal;
                Next();
            }

            yield break;

            void Next() => val = foo?.Invoke();
        }

        private unsafe void CacheMethod(ScriptFunctions method)
        {
            if (_methods != null && _methods.TryGetValue(method, out _))
                return;

            if (_engine == null)
                return;

            _methods ??= new Dictionary<ScriptFunctions, JsValue>();

            var val = _engine.GetValue(method.ToString());
            if (val.IsUndefined())
                return;

            _methods.Add(method, val);
        }

        /// <summary>
        /// Gets the global members that are accessible within any given javascript file.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="context">The context to get the members for.</param>
        /// <returns>The global members.</returns>
        public static unsafe Dictionary<string, object> GetEmbeddedGlobalMembers(Engine engine, MetaverseScript context)
        {
            var ret = new Dictionary<string, object>
            {
                // Context Properties and Methods
                { ThisProperty, context },
                { GameObjectProperty, context.gameObject },
                { TransformProperty, context.transform },
                { ConsoleObjectProperty, context.console },
                { nameof(Vars), context.Vars },
                { GetEnabledFunction, (Func<bool>)(() => context.enabled) },
                { SetEnabledFunction, (Action<bool>)(b => context.enabled = b) },
                { nameof(GetVar), (Func<string, object>)context.GetVar },
                { nameof(SetVar), (Action<string, object>)context.SetVar },
                { nameof(TryGetVar), (Func<string, object, object>)context.TryGetVar },
                { nameof(TrySetVar), (Func<string, object, bool>)context.TrySetVar },
                { nameof(DefineVar), (Func<string, object, Func<object>>)context.DefineVar },
                { nameof(DefineTypedVar), (Func<string, string, object, Func<object>>)context.DefineTypedVar },

                // MetaSpace Property
                { MetaSpaceProperty, MetaSpace.Instance },

                // Global Variable Functions
                { GetGlobalFunction, (Func<string, object>)(k => MetaverseScriptCache.Current.GetStaticReference(k)) },
                {
                    SetGlobalFunction,
                    (Action<string, object>)((k, v) => MetaverseScriptCache.Current.SetStaticReference(k, v))
                },

                // Networking Functions
                { GetNetworkObjectFunction, (Func<NetworkObject>)(() => context.NetworkObject.uNull()) },
                {
                    IsInputAuthorityFunction,
                    (Func<bool>)(() => context.NetworkObject.uNull()?.IsInputAuthority ?? false)
                },
                {
                    IsStateAuthorityFunction,
                    (Func<bool>)(() => context.NetworkObject.uNull()?.IsStateAuthority ?? false)
                },
                { GetHostIDFunction, (Func<int>)(() => context.NetworkObject.uNull()?.Networking.HostID ?? -1) },
                {
                    RegisterRPCFunction,
                    (Action<short, RpcEventDelegate>)((rpc, h) => context.NetworkObject.uNull()?.RegisterRPC(rpc, h))
                },
                {
                    UnregisterRPCFunction,
                    (Action<short, RpcEventDelegate>)((rpc, h) => context.NetworkObject.uNull()?.UnregisterRPC(rpc, h))
                },
                {
                    ServerRPCFunction,
                    (Action<short, object>)((rpc, p) =>
                        context.NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Host, p))
                },
                {
                    ServerRPCBufferedFunction,
                    (Action<short, object>)((rpc, p) =>
                        context.NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Host, p, buffered: true))
                },
                {
                    ClientRPCFunction,
                    (Action<short, object>)((rpc, p) =>
                        context.NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.All, p))
                },
                {
                    ClientRPCBufferedFunction,
                    (Action<short, object>)((rpc, p) =>
                        context.NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.All, p, buffered: true))
                },
                {
                    ClientRPCOthersFunction,
                    (Action<short, object>)((rpc, p) =>
                        context.NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Others, p))
                },
                {
                    ClientRPCOthersBufferedFunction,
                    (Action<short, object>)((rpc, p) =>
                        context.NetworkObject.uNull()
                            ?.InvokeRPC(rpc, NetworkMessageReceivers.Others, p, buffered: true))
                },
                {
                    PlayerRPCFunction,
                    (Action<short, int, object>)((rpc, player, p) =>
                        context.NetworkObject.uNull()?.InvokeRPC(rpc, player, p))
                },
                {
                    SpawnNetworkPrefabFunction,
                    (System.Action<GameObject, Vector3, Quaternion, Transform, Action<GameObject>>)(
                        (pref, pos, rot, parent, cb) =>
                        {
                            if (!pref)
                            {
                                context.console.error($"Cannot spawn null prefab: {pref?.name ?? "null"}");
                                return;
                            }

                            var netSvc = context.MetaSpace.GetService<IMetaSpaceNetworkingService>();
                            if (netSvc == null)
                            {
                                context.console.error("Networking is not available.");
                                return;
                            }

                            var sPos = parent ? parent.InverseTransformPoint(pos) : pos;
                            var sRot = parent ? Quaternion.Inverse(parent.rotation) * rot : rot;
                            netSvc.SpawnGameObject(pref, spawned =>
                            {
                                if (!context || !context.isActiveAndEnabled || spawned.IsStale)
                                {
                                    spawned.IsStale = true;
                                    return;
                                }

                                if (parent) spawned.Transform.parent = parent;
                                spawned.Transform.SetLocalPositionAndRotation(sPos, sRot);
                                cb?.Invoke(spawned.GameObject);
                            }, pos, rot, false);
                        })
                },

                // Timing Functions
                {
                    SetTimeoutFunction, (Func<Action, int, int>)((a, t) =>
                    {
                        if (!context || !context.isActiveAndEnabled)
                            return -1;
                        var h = ++_timeoutHandleIndex;
                        context._timeoutHandles.Add(h);
                        MetaverseDispatcher.WaitForSeconds(t / 1000f, () =>
                        {
                            if (!context || !context.isActiveAndEnabled)
                                return;
                            if (!context._timeoutHandles.Remove(h)) return;
                            try
                            {
                                a?.Invoke();
                            }
                            catch (Exception e)
                            {
                                context.console.error(
                                    $"Error in setTimeout on {(context.javascriptFile ? context.javascriptFile.name : "Missing Script")}: {e.GetBaseException()}");
                            }
                        });
                        return h;
                    })
                },
                { ClearTimeoutFunction, (Action<int>)(h => context._timeoutHandles.Remove(h)) },

                // Coroutine Function
                { CoroutineFunction, (Action<Func<object>>)(o => context.StartCoroutine(CoroutineUpdate(o))) },

                // Utility Functions
                { PrintFunction, (Action<object>)(o => context.console.log(o)) },
                { NewGuidFunction, (Func<string>)(() => Guid.NewGuid().ToString()) },
                { IsUnityNullFunctionOld1, (Func<object, bool>)(o => o.IsUnityNull()) },
                { IsUnityNullFunctionOld2, (Func<object, bool>)(o => o.IsUnityNull()) },
                {
                    GetMetaverseScriptFunction, (Func<string, GameObject, object>)((n, go) =>
                        go.GetComponents<MetaverseScript>()
                            .FirstOrDefault(x => x.javascriptFile && x.javascriptFile.name == n))
                },
                // Async/Await Function
                {
                    AwaitFunction, (Action<object, Action<object>>)((t, a) =>
                    {
                        if (!context || !context.isActiveAndEnabled)
                            return;
                        if (t is not Task task)
                        {
                            if (t is IEnumerator e)
                            {
                                _ = e.ToUniTask().ContinueWith(() =>
                                {
                                    if (!context || !context.isActiveAndEnabled)
                                        return;
                                    a?.Invoke(t);
                                });
                                return;
                            }
#if MV_META_CORE
                            // Support OVRTask.cs
                            if (t is not null && t.GetType().Name.StartsWith(nameof(OVRTask)))
                            {
                                var continueWithMethod = t.GetType()
                                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .FirstOrDefault(x => x.Name == "ContinueWith");
                                
                                // public void ContinueWith(Action<TResult> onCompleted)
                                if (continueWithMethod != null && continueWithMethod.GetParameters().Length == 1)
                                {
                                    // OVRTask<TResult>  →  we need an Action<TResult>
                                    var resultType = t.GetType().GenericTypeArguments[0];
                                    var actionType = typeof(Action<>).MakeGenericType(resultType); // Action<TResult>
                                    var rParam = System.Linq.Expressions.Expression.Parameter(resultType, "r");
                                    var ctxConst = System.Linq.Expressions.Expression.Constant(context, typeof(MonoBehaviour));
                                    var cbConst = System.Linq.Expressions.Expression.Constant(a, typeof(Action<object>));

                                    // (!context || !context.isActiveAndEnabled) ↦ guard expression
                                    var guard = System.Linq.Expressions.Expression.OrElse(
                                        System.Linq.Expressions.Expression.Equal(
                                            ctxConst, 
                                            System.Linq.Expressions.Expression.Constant(null, typeof(MonoBehaviour))),
                                        System.Linq.Expressions.Expression.Not(
                                            System.Linq.Expressions.Expression.Property(ctxConst, nameof(isActiveAndEnabled)))
                                    );
                                    var body = System.Linq.Expressions.Expression.IfThenElse(
                                        guard,
                                        System.Linq.Expressions.Expression.Empty(), // early-out
                                        System.Linq.Expressions.Expression.Invoke(cbConst,
                                            System.Linq.Expressions.Expression.Convert(rParam, typeof(object)))
                                    );

                                    // finally: ovrTask.ContinueWith(lambda);
                                    var lambda = System.Linq.Expressions.Expression.Lambda(actionType, body, rParam).Compile();
                                    continueWithMethod.Invoke(t, new object[] { lambda });
                                }
                                else
                                {
                                    context.console.error("OVRTask ContinueWith method not found or invalid parameters.");
                                }
                                // ReSharper disable once RedundantJumpStatement
                                return;
                            }
#endif
                            return;
                        }

                        if (task.GetType().GenericTypeArguments.Length == 0)
                        {
                            _ = task.AsUniTask().ContinueWith(() =>
                            {
                                if (!context || !context.isActiveAndEnabled)
                                    return;
                                a(task);
                            });
                            return;
                        }

                        const string asUniTaskName = "AsUniTask";
                        var asUniTask = task.GetType()
                            .GetExtensionMethods()
                            .FirstOrDefault(x =>
                                x.Name == asUniTaskName &&
                                x.GetParameters().Length == 2 &&
                                x.IsGenericMethod &&
                                x.ReturnType == typeof(UniTask));
                        if (asUniTask == null) return;
                        const string continueWithName = "ContinueWith";
                        var uniTask = asUniTask.Invoke(null, new[] { t, true });
                        var continueWith = uniTask
                            .GetType()
                            .GetExtensionMethods()
                            .FirstOrDefault(x => x.Name == continueWithName && x.ReturnType == typeof(UniTask));
                        if (continueWith == null) return;
                        _ = continueWith.Invoke(uniTask, new[]
                        {
                            uniTask,
                            (Action<object>)((t1) =>
                            {
                                if (!context || !context.isActiveAndEnabled)
                                    return;
                                a(t1);
                            })
                        });
                    })
                },

                // Show Dialog
                {
                    AlertFunction, (System.Action<string>)(message =>
                    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                    MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog("Alert", message, "OK", () => {},
                        force: true);
#endif
                    })
                },
                {
                    ShowDialogFunction,
                    (System.Action<string, string, string, Action>)((title, message, ok, okCallback) =>
                    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                    MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(title, message, ok, okCallback);
#endif
                    })
                },
                {
                    ShowForcedDialogFunction,
                    (System.Action<string, string, string, Action>)((title, message, ok, okCallback) =>
                    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                    MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(title, message, ok, okCallback,
                        force: true);
#endif
                    })
                },
                {
                    ShowDialogComplexFunction,
                    (System.Action<string, string, string, string, Action, Action>)((title, message, ok, cancel,
                        okCallback,
                        cancelCallback) =>
                    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                    MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(title, message, ok, cancel,
                        okCallback, cancelCallback);
#endif
                    })
                },
                {
                    ShowForcedDialogComplexFunction,
                    (System.Action<string, string, string, string, Action, Action>)((title, message, ok, cancel,
                        okCallback,
                        cancelCallback) =>
                    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                    MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(title, message, ok, cancel,
                        okCallback, cancelCallback, force: true);
#endif
                    })
                },
            };
            
#if MV_META_CORE
            if (context && context.globalTypeImports.HasFlag(GlobalTypeImports.MetaQuest))
            {
                _ovrTypes ??= AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(x => x.FullName.Contains("Oculus.VR") && !x.FullName.Contains("Editor"))?
                    .GetTypes()
                    .Where(t => 
                        string.IsNullOrEmpty(t.Namespace) && 
                        t.IsPublic && 
                        !t.Name.Contains("`") &&
                        t.Name.StartsWith("OVR") &&
                        t.GenericTypeArguments.Length == 0)
                    .ToArray() ?? Array.Empty<Type>();

                if (_ovrTypes != null)
                    foreach (var ovrClass in _ovrTypes)
                    {
                        ret[ovrClass.Name] = 
                            Jint.Runtime.Interop.TypeReference.CreateTypeReference(
                                engine, ovrClass);
                    }
            }
#endif

            return ret;
        }

        private static Type[] _ovrTypes = null;

        /// <summary>
        /// Gets the global member types that are accessible within any given javascript file.
        /// </summary>
        /// <returns>The global member types.</returns>
        public static unsafe Dictionary<string, Type> GetEmbeddedGlobalMemberTypeMap() => new()
        {
            // Context Properties and Methods
            { ThisProperty, typeof(MetaverseScript) },
            { GameObjectProperty, typeof(GameObject) },
            { TransformProperty, typeof(Transform) },
            { ConsoleObjectProperty, typeof(ConsoleObject) },
            { nameof(Vars), typeof(VariableDeclarations) },
            { GetEnabledFunction, typeof(Func<bool>) },
            { SetEnabledFunction, typeof(Action<bool>) },
            { nameof(GetVar), typeof(Func<string, object>) },
            { nameof(SetVar), typeof(Action<string, object>) },
            { nameof(TryGetVar), typeof(Func<string, object, object>) },
            { nameof(TrySetVar), typeof(Func<string, object, bool>) },
            { nameof(DefineVar), typeof(Func<string, object, Func<object>>) },
            { nameof(DefineTypedVar), typeof(Func<string, string, object, Func<object>>) },

            // MetaSpace Property
            { MetaSpaceProperty, typeof(MetaSpace) },

            // Global Variable Functions
            { GetGlobalFunction, typeof(Func<string, object>) },
            { SetGlobalFunction, typeof(Action<string, object>) },

            // Networking Functions
            { GetNetworkObjectFunction, typeof(Func<NetworkObject>) },
            { IsInputAuthorityFunction, typeof(Func<bool>) },
            { IsStateAuthorityFunction, typeof(Func<bool>) },
            { GetHostIDFunction, typeof(Func<int>) },
            { RegisterRPCFunction, typeof(Action<short, RpcEventDelegate>) },
            { UnregisterRPCFunction, typeof(Action<short, RpcEventDelegate>) },
            { ServerRPCFunction, typeof(Action<short, object>) },
            { ServerRPCBufferedFunction, typeof(Action<short, object>) },
            { ClientRPCFunction, typeof(Action<short, object>) },
            { ClientRPCBufferedFunction, typeof(Action<short, object>) },
            { ClientRPCOthersFunction, typeof(Action<short, object>) },
            { ClientRPCOthersBufferedFunction, typeof(Action<short, object>) },
            { PlayerRPCFunction, typeof(Action<short, int, object>) },
            {
                SpawnNetworkPrefabFunction,
                typeof(System.Action<GameObject, Vector3, Quaternion, Transform, Action<GameObject>>)
            },

            // Timing Functions
            { SetTimeoutFunction, typeof(Func<Action, int, int>) },
            { ClearTimeoutFunction, typeof(Action<int>) },

            // Coroutine Function
            { CoroutineFunction, typeof(Action<Func<object>>) },

            // Utility Functions
            { PrintFunction, typeof(Action<object>) },
            { NewGuidFunction, typeof(Func<string>) },
            { IsUnityNullFunctionOld1, typeof(Func<object, bool>) },
            { IsUnityNullFunctionOld2, typeof(Func<object, bool>) },
            { GetMetaverseScriptFunction, typeof(Func<string, GameObject, object>) },

            // Async/Await Function
            { AwaitFunction, typeof(Action<object, Action<object>>) },

            // Show Dialog
            { AlertFunction, typeof(System.Action<string>) },
            { ShowDialogFunction, typeof(System.Action<string, string, string, Action>) },
            { ShowForcedDialogFunction, typeof(System.Action<string, string, string, Action>) },
            { ShowDialogComplexFunction, typeof(System.Action<string, string, string, string, Action, Action>) },
            { ShowForcedDialogComplexFunction, typeof(System.Action<string, string, string, string, Action, Action>) },
        };

        /// <summary>
        /// Gets the types that contain extension methods.
        /// </summary>
        /// <returns>The types that contain extension methods.</returns>
        public static unsafe Type[] GetExtensionMethodTypes()
        {
            return new[]
            {
                typeof(Enumerable),
                typeof(MVUtils),
                typeof(MetaverseDispatcherExtensions),
                typeof(UniTaskExtensions),
                typeof(ImageConversion)
#if MV_UNITY_AR_CORE && MV_AR_CORE_EXTENSIONS && ((UNITY_IOS || UNITY_ANDROID) || UNITY_EDITOR)
                , typeof(Google.XR.ARCoreExtensions.ARAnchorManagerExtensions)
#endif
#if MV_UNITY_AI_INFERENCE
                , typeof(CommandBufferWorkerExtensions)
                , typeof(FunctionalExtensions)
                , typeof(Functional)
                , typeof(TextureConverter)
#endif
            };
        }

        /// <summary>
        /// Gets the assemblies to allow access to from javascript.
        /// </summary>
        /// <returns>The assemblies to allow access to.</returns>
        public static unsafe Assembly[] GetAssemblies()
        {
            var assemblies = new[]
            {
                typeof(DateTime).Assembly,
                typeof(Transform).Assembly,
                typeof(GameObject).Assembly,
                typeof(Component).Assembly /* UnityEngine.CoreModule */,
                typeof(Rigidbody).Assembly /* UnityEngine.PhysicsModule */,
                typeof(RaycastHit).Assembly /* UnityEngine.PhysicsModule */,
                typeof(RaycastHit2D).Assembly /* UnityEngine.PhysicsModule */,
                typeof(Terrain).Assembly /* UnityEngine.TerrainModule */,
                typeof(AudioSource).Assembly /* UnityEngine.AudioModule */,
                typeof(Canvas).Assembly /* UnityEngine.UIModule */,
                typeof(RaycastResult).Assembly /* UnityEngine.UI */,
#if MV_UNITY_AI_NAV
                typeof(UnityEngine.AI.NavMesh).Assembly /* UnityEngine.AIModule */,
                typeof(UnityEngine.AI.NavMeshAgent).Assembly /* UnityEngine.AIModule */,
                typeof(NavMeshSurface).Assembly /* Unity.AI.Navigation */,
#endif
                typeof(Input).Assembly /* UnityEngine.InputModule */,
                typeof(MetaverseProgram).Assembly /* MetaverseCloudEngine */,
                typeof(MetaverseClient).Assembly /* MetaverseCloudEngine.ApiClient */,
                typeof(MetaSpaceDto).Assembly /* MetaverseCloudEngine.Common */,
                typeof(TextMeshPro).Assembly /* TextMeshPro */,
                typeof(InputSystem).Assembly /* New Input System */,
                typeof(CinemachineCore).Assembly /* Cinema-chine */,
                typeof(Variables).Assembly /* Visual Scripting */,
                typeof(System.Collections.Concurrent.BlockingCollection<>).Assembly /* System.Collections.Concurrent */,
#if MV_XR_TOOLKIT
#pragma warning disable CS0618 // Type or member is obsolete
                typeof(UnityEngine.XR.Interaction.Toolkit.ActionBasedController).Assembly /* XR Interaction Toolkit */,
#pragma warning restore CS0618 // Type or member is obsolete
#endif
                typeof(Task).Assembly /* System.Threading.Tasks */,
                typeof(UniTask).Assembly /* UniTask */,
                typeof(UniTaskExtensions).Assembly, /* UniTask */
                typeof(NativeArray<>).Assembly /* Unity.Collections */
#if MV_META_CORE // Oculus.VR
                , typeof(OVRSpatialAnchor).Assembly
                , typeof(OVRManager).Assembly
                , typeof(OVRInput).Assembly
#endif
#if MV_XRCOREUTILS
                , typeof(XROrigin).Assembly
#endif
#if MV_XR_LEGACY_INPUT_HELPERS
                , typeof(UnityEngine.SpatialTracking.TrackedPoseDriver).Assembly /* UnityEngine.SpatialTracking.dll */
#endif
#if MV_PTC_VUFORIA && !UNITY_WEBGL && !UNITY_STANDALONE_LINUX
                , typeof(Vuforia.VuforiaApplication).Assembly
                , typeof(Vuforia.VuforiaConfiguration).Assembly
#endif
#if MV_UNITY_AR_FOUNDATION && (UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR)
                , typeof(UnityEngine.XR.ARSubsystems.XRRaycastHit).Assembly
                , typeof(UnityEngine.XR.ARFoundation.ARRaycastHit).Assembly
#endif
#if MV_UNITY_AR_CORE && (UNITY_ANDROID || UNITY_EDITOR)
                , typeof(UnityEngine.XR.ARCore.ARCoreSessionSubsystem).Assembly
#endif
#if MV_UNITY_AR_CORE && MV_AR_CORE_EXTENSIONS && ((UNITY_IOS || UNITY_ANDROID) || UNITY_EDITOR)
                , typeof(Google.XR.ARCoreExtensions.ARAnchorManagerExtensions).Assembly
                , typeof(Google.XR.ARCoreExtensions.ARStreetscapeGeometryManager).Assembly
                , typeof(Google.XR.ARCoreExtensions.GeospatialCreator.ARGeospatialCreatorOrigin).Assembly
#if MV_CESIUM_UNITY
                , typeof(CesiumForUnity.CesiumGeoreference).Assembly
#endif
#endif
#if MV_UNITY_AR_KIT && (UNITY_IOS || UNITY_EDITOR)
                , typeof(UnityEngine.XR.ARKit.ARKitSessionSubsystem).Assembly
#endif
                , typeof(CoordinateSharp.Coordinate).Assembly
                , typeof(CoordinateSharp.Magnetic.Magnetic).Assembly
#if MV_UNITY_AI_INFERENCE
                , typeof(ModelAsset).Assembly
                , typeof(CommandBufferWorkerExtensions).Assembly
                , typeof(FunctionalExtensions).Assembly
                , typeof(Functional).Assembly
                , typeof(TextureConverter).Assembly
#endif
#if MV_ROOM_PLAN_UNITY_KIT
                , typeof(SilverTau.Utilities.CSGeometry).Assembly
                , typeof(SilverTau.ConcaveHull.Hull).Assembly
                , typeof(SilverTau.CSGeometry.CSGeometryWorker).Assembly
                , typeof(SilverTau.CSGeometry.v2.BinarySpacePartitioningHelper).Assembly
                , typeof(SilverTau.RoomPlanUnity.CapturedObjectAttributes).Assembly
                , typeof(SilverTau.Triangulation.AdvancingFront).Assembly
#endif
            };
            return assemblies
                .Concat(GetExtensionMethodTypes().Select(x => x.Assembly).Distinct())
                .ToArray();
        }
    }
}