using Jint;
using Jint.Native;
using TMPro;
using Cinemachine;
using Cysharp.Threading.Tasks;

using CesiumForUnity;

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
#if MV_UNITY_AI_NAV
using Unity.AI.Navigation;
#endif

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// A component that's used to execute javascript with Unity-style functions.
    /// By default, Unity only supports C# scripts, but this component allows you to write scripts in javascript
    /// using the Jint library.
    /// </summary>
    [HideMonoScript]
    [HelpURL("https://reach-cloud.gitbook.io/reach-explorer-documentation/docs/development-guide/unity-engine-sdk/custom-scripting/custom-javascript")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Scripting/Metaverse Script")]
    [ExecuteAlways]
    public class MetaverseScript : NetworkObjectBehaviour
    {
#pragma warning disable CS0618
        private static readonly List<string> BlackListedNames = new()
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
            nameof(DontDestroyOnLoad),
            nameof(Input.location),
        };
#pragma warning restore CS0618

        private static readonly List<string> BlackListedNamespaces = new()
        {
            "System.IO",
            "System.Reflection",
            "System.Web",
            "System.Http",
            "System.CodeDom",
        };

        private static readonly List<string> BlackListedTypes = new()
        {
            nameof(Resources),
            nameof(AssetBundle),
            nameof(PlayerPrefs),
        };

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
        }

        private const string ThisProperty = "_this";
        private const string GameObjectProperty = "gameObject";
        private const string TransformProperty = "transform";
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
        private const string IsInputAuthorityProperty = "GetIsInputAuthority";
        private const string IsStateAuthorityProperty = "GetIsStateAuthority";
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

        [Tooltip("The file that contains the javascript.")]
        [Required] public TextAsset javascriptFile;
        [SerializeField] private TextAsset[] includes;
        [SerializeField] private Variables variables;

        private bool _ready;
        private Engine _engine;
        private Dictionary<ScriptFunctions, JsValue> _methods;
        private readonly Dictionary<string, JsValue> _functionLookup = new();
        private static int _timeoutHandleIndex;
        private readonly HashSet<int> _timeoutHandles = new();
        private readonly Queue<Action> _initializationMethodQueue = new(); 

        /// <summary>
        /// Gets the variable declarations for the javascript file.
        /// </summary>
        public VariableDeclarations Vars => variables ? variables.declarations : null;

        protected override void OnDestroy()
        {
            _initializationMethodQueue.Clear(); // Make sure no initialization methods are triggered.
            
            base.OnDestroy();
            
            if (_methods != null && _ready)
            {
                if (_methods?.TryGetValue(ScriptFunctions.OnDestroy, out var method) == true)
                    _ = _engine.Invoke(method);
            }

            _engine?.Dispose();
        }

        private void OnEnable()
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnEnable, out var method) == true)
                _ = _engine.Invoke(method);
        }

        private void OnDisable()
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnDisable, out var method) == true)
                _ = _engine.Invoke(method);
        }

        private void Start()
        {
            try
            {
                if (!Application.isPlaying)
                {
                    return;
                }

                if (MetaSpace.Instance)
                {
                    MetaSpace.OnReady(() => OnMetaSpaceReady());
                    return;
                }

                OnMetaSpaceReady();
            }
            catch(Exception e)
            {
                MetaverseProgram.Logger.LogError("Failed to initialize Metaverse Script: " + e);
                enabled = false;
            }
            return;

            void OnMetaSpaceReady()
            {
                MetaverseDispatcher.AtEndOfFrame(() =>
                {
                    try
                    {
                        if (!TryInitializeEngine())
                        {
                            enabled = false;
                            return;
                        }

                        void CallAwake()
                        {
                            _ready = true;
                            
                            if (_methods?.TryGetValue(ScriptFunctions.Awake, out var awakeMethod) == true)
                                _ = _engine.Invoke(awakeMethod);   
                            
                            while (_initializationMethodQueue.Count > 0)
                            {
                                try
                                {
                                    _initializationMethodQueue.Dequeue()?.Invoke();
                                }
                                catch (Exception e)
                                {
                                    MetaverseProgram.Logger.LogError("Failed to execute initialization method: " + e);
                                }
                            }
                        }

                        if (gameObject.activeInHierarchy)
                        {
                            CallAwake();
                        }
                        else
                        {
                            MetaverseDispatcher.WaitUntil(() => !this || gameObject.activeInHierarchy, () =>
                            {
                                if (!this) return;
                                CallAwake();
                            });
                        }
                        
                        void CallOnEnabled()
                        {
                            if (_methods?.TryGetValue(ScriptFunctions.OnEnable, out var onEnableMethod) == true)
                                _ = _engine.Invoke(onEnableMethod);

                            if (enabled && _methods?.TryGetValue(ScriptFunctions.Start, out var startMethod) == true)
                                _ = _engine.Invoke(startMethod);
                        }

                        if (isActiveAndEnabled)
                        {
                            CallOnEnabled();
                        }
                        else
                        {
                            MetaverseDispatcher.WaitUntil(() => !this || isActiveAndEnabled, () =>
                            {
                                if (!this) return;
                                CallOnEnabled();
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        MetaverseProgram.Logger.LogError("Failed to initialize MetaverseScript '" + name + "': " + e);
                        enabled = false;
                    }
                });
            }
        }

        private void Update()
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.Update, out var method) == true)
                _ = _engine.Invoke(method);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.LateUpdate, out var method) == true)
                _ = _engine.Invoke(method);
        }

        private void FixedUpdate()
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.FixedUpdate, out var method) == true)
                _ = _engine.Invoke(method);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerEnter, out var method) == true)
                _ = _engine.Invoke(method, other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerExit, out var method) == true)
                _ = _engine.Invoke(method, other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerStay, out var method) == true)
                _ = _engine.Invoke(method, other);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerEnter2D, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerExit2D, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnTriggerStay2D, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnAnimatorIK(int layer)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnAnimatorIK, out var method) == true)
                _ = _engine.Invoke(method, layer);
        }

        private void OnAnimatorMove()
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnAnimatorMove, out var method) == true)
                _ = _engine.Invoke(method);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionEnter, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionExit, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionStay, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionEnter2D, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionExit2D, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnCollisionStay2D, out var method) == true)
                _ = _engine.Invoke(method, collision);
        }

        public override void OnNetworkReady(bool offline)
        {
            base.OnNetworkReady(offline);
            OnEngineReady(() =>
            {
                if (_methods?.TryGetValue(ScriptFunctions.OnNetworkReady, out var method) == true)
                    _ = _engine.Invoke(method, offline); 
            });
        }

        protected override void RegisterNetworkRPCs()
        {
            base.RegisterNetworkRPCs();
            OnEngineReady(() =>
            {
                if (_methods?.TryGetValue(ScriptFunctions.RegisterNetworkRPCs, out var method) == true)
                    _ = _engine.Invoke(method); 
            });
        }

        protected override void UnRegisterNetworkRPCs()
        {
            base.UnRegisterNetworkRPCs();
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.UnRegisterNetworkRPCs, out var method) == true)
                _ = _engine.Invoke(method);
        }

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();
            OnEngineReady(() =>
            {
                if (_methods?.TryGetValue(ScriptFunctions.OnMetaSpaceBehaviourInitialize, out var method) == true)
                    _ = _engine.Invoke(method);
            });
        }

        protected override void OnMetaSpaceServicesRegistered()
        {
            base.OnMetaSpaceServicesRegistered();
            OnEngineReady(() =>
            {
                if (_methods?.TryGetValue(ScriptFunctions.OnMetaSpaceServicesRegistered, out var method) == true)
                    _ = _engine.Invoke(method); 
            });
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            base.OnMetaSpaceBehaviourDestroyed();
            if (!_ready) return;
            if (_methods?.TryGetValue(ScriptFunctions.OnMetaSpaceBehaviourDestroyed, out var method) == true)
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
        public void ExecuteVoid(string fn)
        {
            if (string.IsNullOrEmpty(fn))
                return;

            if (!_ready)
            {
                MetaverseProgram.Logger.Log($"The script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
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
        public void ExecuteVoid(string fn, object[] args)
        {
            if (string.IsNullOrEmpty(fn))
                return;

            if (!_ready)
            {
                MetaverseProgram.Logger.Log($"The script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
                return;
            }
            
            if (!_functionLookup.TryGetValue(fn, out var method))
                _functionLookup[fn] = method = _engine.GetValue(fn);
            
            if (method != null && !method.IsUndefined())
                _ = _engine.Invoke(method, args);
        }

        /// <summary>
        /// Executes a javascript function with arguments.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        /// <param name="arguments">The arguments to pass to the function.</param>
        /// <returns>The result of the function.</returns>
        public JsValue Execute(string fn, object[] arguments)
        {
            if (string.IsNullOrEmpty(fn))
                return null;

            if (!_ready)
            {
                MetaverseProgram.Logger.Log($"The script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
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
        public JsValue Execute(string fn)
        {
            if (string.IsNullOrEmpty(fn))
                return null;

            if (!_ready)
            {
                MetaverseProgram.Logger.Log($"The script '{javascriptFile?.name ?? ""}' has not fully initialized yet. Call to '{fn}' ignored.");
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
        public object GetVar(string variableName)
        {
            return TryGetVar(variableName, null);
        }

        /// <summary>
        /// Gets a Unity variable with the given name, or a default value if it doesn't exist.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="defaultValue">The default value to return if the variable doesn't exist.</param>
        /// <returns>The variable value.</returns>
        public object TryGetVar(string variableName, object defaultValue)
        {
            if (variables == null) return defaultValue;
            return variables.declarations.IsDefined(variableName) ? variables.declarations.Get(variableName) : defaultValue;
        }
        
        /// <summary>
        /// Sets a Unity variable with the given name.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="value">The value to set it to.</param>
        public void SetVar(string variableName, object value)
        {
            if (variables == null) return;
            variables.declarations.Set(variableName, value);
        }
        
        /// <summary>
        /// Tries to set a Unity variable with the given name.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="value">The value to set it to.</param>
        /// <returns>true if the variable was set, false otherwise.</returns>
        public bool TrySetVar(string variableName, object value)
        {
            if (variables == null) return false;
            if (!variables.declarations.IsDefined(variableName)) return false;
            variables.declarations.Set(variableName, value);
            return true;
        }
        
        /// <summary>
        /// Gets a property with the given name from the javascript engine.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The property value.</returns>
        public JsValue GetProperty(string propertyName)
        {
            return GetProperty(propertyName, JsValue.Undefined);
        }

        /// <summary>
        /// Gets a property with the given name from the javascript engine.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="defaultValue">The default value to return if the property doesn't exist.</param>
        /// <returns>The property value.</returns>
        public JsValue GetProperty(string propertyName, JsValue defaultValue)
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
        public bool SetProperty(string propertyName, object value)
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
        public static bool IsMemberAllowed(MemberInfo member)
        {
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
        public static bool IsInBlackListedNamespace(MemberInfo member)
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
        public static bool IsBlackListedMemberName(string value, bool isType = false)
        {
            return BlackListedNames.Contains(value) || (isType && BlackListedTypes.Contains(value));
        }

        private Func<object> DefineVar(string variableName, object defaultValue)
        {
            return () => TryGetVar(variableName, defaultValue);
        }
        
        private Func<object> DefineTypedVar(string variableName, string typePath, object defaultValue)
        {
            return () =>
            {
                var output = TryGetVar(variableName, defaultValue);
                if (output is UnityEngine.Object o && !o)
                    return null;
                return output;
            };
        }

        private void OnEngineReady(Action a)
        {
            if (_ready)
            {
                a?.Invoke();
                return;
            }
            
            _initializationMethodQueue.Enqueue(a);
        }

        private bool TryInitializeEngine()
        {
            if (!javascriptFile)
                return false;

            _engine = new Engine(o => DefaultEngineOptions(o, true))
                .SetValue(GetGlobalFunction, (Func<string, object>)(key => MetaverseScriptCache.Current.GetStaticReference(key)))
                .SetValue(SetGlobalFunction, (Action<string, object>)((key, value) => MetaverseScriptCache.Current.SetStaticReference(key, value)))
                .SetValue(PrintFunction, (Action<object>)(o => MetaverseProgram.Logger.Log(o)))
                .SetValue(NewGuidFunction, (Func<string>)(() => Guid.NewGuid().ToString()))
                .SetValue(MetaSpaceProperty, (object)MetaSpace.Instance)
                .SetValue(GetMetaverseScriptFunction, (Func<string, GameObject, object>)((n, go) => go.GetComponents<MetaverseScript>().FirstOrDefault(x => x.javascriptFile && x.javascriptFile.name == n)))
                .SetValue(ThisProperty, (object)this)
                .SetValue(GameObjectProperty, (object)gameObject)
                .SetValue(TransformProperty, (object)transform)
                .SetValue(CoroutineFunction, (Action<Func<object>>)(o => StartCoroutine(CoroutineUpdate(o))))
                .SetValue(GetEnabledFunction, (Func<bool>)(() => enabled))
                .SetValue(nameof(Vars), Vars)
                .SetValue(SetEnabledFunction, (Action<bool>)(b => enabled = b))
                .SetValue(nameof(GetVar), (Func<string, object>)GetVar)
                .SetValue(nameof(TryGetVar), (Func<string, object, object>)TryGetVar)
                .SetValue(nameof(SetVar), (Action<string, object>)SetVar)
                .SetValue(nameof(TrySetVar), (Func<string, object, bool>)TrySetVar)
                .SetValue(nameof(DefineVar), (Func<string, object, Func<object>>)DefineVar)
                .SetValue(nameof(DefineTypedVar), (Func<string, string, object, Func<object>>)DefineTypedVar)
                .SetValue(GetNetworkObjectFunction, (Func<NetworkObject>)(() => NetworkObject.uNull()))
                .SetValue(IsInputAuthorityProperty, (Func<bool>)(() => NetworkObject.uNull()?.IsInputAuthority ?? false))
                .SetValue(IsStateAuthorityProperty, (Func<bool>)(() => NetworkObject.uNull()?.IsStateAuthority ?? false))
                .SetValue(SetTimeoutFunction, (Func<Action, int, int>)((action, time) =>
                {
                    var timeoutHandle = ++_timeoutHandleIndex;
                    _ = _timeoutHandles.Add(timeoutHandle);
                    MetaverseDispatcher.WaitForSeconds(time / 1000f, () =>
                    {
                        if (!_timeoutHandles.Remove(timeoutHandle)) return;
                        if (!this) return;
                        try
                        {
                            action?.Invoke();
                        }
                        catch(Exception e)
                        {
                            MetaverseProgram.Logger.LogError($"Error ocurred in timeout ({timeoutHandle}): {e}");
                        }
                    });
                    return timeoutHandle;
                }))
                .SetValue(ClearTimeoutFunction, (Action<int>)(handle => { _ = _timeoutHandles.Remove(handle); }))
                .SetValue(RegisterRPCFunction, (Action<short, RpcEventDelegate>)((rpc, handler) => { NetworkObject.uNull()?.RegisterRPC(rpc, handler); }))
                .SetValue(UnregisterRPCFunction, (Action<short, RpcEventDelegate>)((rpc, handler) => { NetworkObject.uNull()?.UnregisterRPC(rpc, handler); }))
                .SetValue(ServerRPCFunction, (Action<short, object>)((rpc, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Host, parameters); }))
                .SetValue(ServerRPCBufferedFunction, (Action<short, object>)((rpc, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Host, parameters, buffered: true); }))
                .SetValue(ClientRPCFunction, (Action<short, object>)((rpc, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.All, parameters); }))
                .SetValue(ClientRPCBufferedFunction, (Action<short, object>)((rpc, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.All, parameters, buffered: true); }))
                .SetValue(ClientRPCOthersFunction, (Action<short, object>)((rpc, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Others, parameters); }))
                .SetValue(ClientRPCOthersBufferedFunction, (Action<short, object>)((rpc, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, NetworkMessageReceivers.Others, parameters, buffered: true); }))
                .SetValue(PlayerRPCFunction, (Action<short, int, object>)((rpc, player, parameters) => { NetworkObject.uNull()?.InvokeRPC(rpc, player, parameters); }))
                .SetValue(GetHostIDFunction, (Func<int>)(() => NetworkObject.uNull()?.Networking.HostID ?? -1))
                .SetValue(SpawnNetworkPrefabFunction, (System.Action<GameObject, Vector3, Quaternion, Transform, Action<GameObject>>)(
                    (pref, pos, rot, parent, callback) =>
                    {
                        if (!pref)
                        {
                            MetaverseProgram.Logger.LogError("Cannot spawn null prefab.");
                            return;
                        }

                        var networkingService = MetaSpace.GetService<IMetaSpaceNetworkingService>();
                        if (networkingService is null)
                        {
                            MetaverseProgram.Logger.LogError("Networking is not available.");
                            return;
                        }

                        var spawnPos = parent ? parent.InverseTransformPoint(pos) : pos;
                        var spawnRot = parent ? Quaternion.Inverse(parent.rotation) * rot : rot;
                        networkingService.SpawnGameObject(pref, spawned =>
                        {
                            if (!this || !isActiveAndEnabled || spawned.IsStale)
                            {
                                spawned.IsStale = true;
                                return;
                            }
                            if (parent) spawned.Transform.parent = parent;
                            spawned.Transform.SetLocalPositionAndRotation(spawnPos, spawnRot);
                            callback?.Invoke(spawned.GameObject);
                            
                        }, pos, rot, false);
                    }))
                    .SetValue(IsUnityNullFunctionOld1, (Func<object, bool>)(o => o.IsUnityNull()))
                    .SetValue(IsUnityNullFunctionOld2, (Func<object, bool>)(o => o.IsUnityNull()))
                    .SetValue(AwaitFunction, (Action<object, Action<object>>)((t, action) =>
                    {
                        if (t is not Task task)
                        {
                            if (t is IEnumerator e)
                                _ = e.ToUniTask().ContinueWith(() => { action?.Invoke(t); });
                            return;
                        }
                        
                        if (task.GetType().GenericTypeArguments.Length == 0)
                        {
                            _ = task.AsUniTask().ContinueWith(() => action);
                            return;
                        }
                        
                        const string asUniTaskFunctionName = "AsUniTask";
                        var asUniTask = task.GetType()
                            .GetExtensionMethods()
                            .FirstOrDefault(x => x.Name == asUniTaskFunctionName && x.GetParameters().Length == 2 && x.IsGenericMethod && x.ReturnType == typeof(UniTask));
                        if (asUniTask is null) 
                            return;
                        
                        const string continueWithFunctionName = "ContinueWith";
                        var uniTask = asUniTask.Invoke(null, new [] { t, true });
                        var continueWith = uniTask
                            .GetType()
                            .GetExtensionMethods()
                            .FirstOrDefault(x => x.Name == continueWithFunctionName && x.ReturnType == typeof(UniTask));
                        if (continueWith is null) 
                            return;

                        _ = continueWith.Invoke(uniTask, new[] { uniTask, action });
                    }));
            
            foreach (var include in includes)
                if (include && !string.IsNullOrEmpty(include.text))
                    _ = _engine.Execute(MetaverseScriptCache.Current.GetScript(include));
            _ = _engine.Execute(MetaverseScriptCache.Current.GetScript(javascriptFile));
            
            var methods = (ScriptFunctions[])Enum.GetValues(typeof(ScriptFunctions));
            foreach (var method in methods)
                CacheMethod(method);

            return _methods != null;
        }

        private static void DefaultEngineOptions(Options options, bool strict)
        {
            options.AllowClr(GetAssemblies())
                .AllowClrWrite()
                .AllowOperatorOverloading()
                .SetTypeResolver(new Jint.Runtime.Interop.TypeResolver
                {
                    MemberFilter = IsMemberAllowed
                })
                .AddExtensionMethods(GetExtensionMethodTypes())
                .CatchClrExceptions(OnJavaScriptCLRException);

            if (strict)
                options.Strict();

            //options.Interop.TrackObjectWrapperIdentity = false;
        }

        /// <summary>
        /// Gets the types that contain extension methods.
        /// </summary>
        /// <returns>The types that contain extension methods.</returns>
        public static Type[] GetExtensionMethodTypes()
        {
            return new [] { typeof(Enumerable), typeof(MVUtils), typeof(MetaverseDispatcherExtensions), typeof(UniTaskExtensions)
#if MV_UNITY_AR_CORE && MV_AR_CORE_EXTENSIONS && ((UNITY_IOS || UNITY_ANDROID) || UNITY_EDITOR) 
                ,typeof(Google.XR.ARCoreExtensions.ARAnchorManagerExtensions)
#endif
            };
        }

        /// <summary>
        /// Gets the assemblies to allow access to from javascript.
        /// </summary>
        /// <returns>The assemblies to allow access to.</returns>
        public static Assembly[] GetAssemblies()
        {
            var assemblies = new [] { 
                typeof(DateTime).Assembly,
                typeof(Transform).Assembly,
                typeof(GameObject).Assembly,
                typeof(Component).Assembly /* UnityEngine.CoreModule */,
                typeof(Rigidbody).Assembly /* UnityEngine.PhysicsModule */,
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
                typeof(UnityEngine.XR.Interaction.Toolkit.ActionBasedController).Assembly /* XR Interaction Toolkit */,
                typeof(Task).Assembly /* System.Threading.Tasks */,
                typeof(UniTask).Assembly /* UniTask */,
                typeof(UniTaskExtensions).Assembly /* UniTask */
#if MV_PTC_VUFORIA && !UNITY_WEBGL && !UNITY_STANDALONE_LINUX
                ,typeof(Vuforia.VuforiaApplication).Assembly
                ,typeof(Vuforia.VuforiaConfiguration).Assembly
#endif
#if MV_UNITY_AR_FOUNDATION && (UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR)
                ,typeof(UnityEngine.XR.ARSubsystems.XRRaycastHit).Assembly
                ,typeof(UnityEngine.XR.ARFoundation.ARRaycastHit).Assembly
#endif
#if MV_UNITY_AR_CORE && (UNITY_ANDROID || UNITY_EDITOR)
                ,typeof(UnityEngine.XR.ARCore.ARCoreSessionSubsystem).Assembly
#endif
#if MV_UNITY_AR_CORE && MV_AR_CORE_EXTENSIONS && ((UNITY_IOS || UNITY_ANDROID) || UNITY_EDITOR)
                ,typeof(Google.XR.ARCoreExtensions.ARAnchorManagerExtensions).Assembly
                ,typeof(Google.XR.ARCoreExtensions.ARStreetscapeGeometryManager).Assembly
                ,typeof(Google.XR.ARCoreExtensions.GeospatialCreator.ARGeospatialCreatorOrigin).Assembly
#if MV_CESIUM_UNITY
                ,typeof(CesiumGeoreference).Assembly
#endif
#endif
#if MV_UNITY_AR_KIT && (UNITY_IOS || UNITY_EDITOR)
                ,typeof(UnityEngine.XR.ARKit.ARKitSessionSubsystem).Assembly
#endif
            };
            return assemblies
                .Concat(GetExtensionMethodTypes().Select(x => x.Assembly).Distinct())
                .ToArray();
        }

        private static bool OnJavaScriptCLRException(Exception exception)
        {
            MetaverseProgram.Logger.LogError("Error in JavaScript CLR: " + exception);
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

        private void CacheMethod(ScriptFunctions method)
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
    }
}
