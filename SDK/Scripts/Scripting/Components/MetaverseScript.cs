using Jint;
using Jint.Native;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
using UnityEngine;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using TMPro;
using Cinemachine;
using Cysharp.Threading.Tasks;
using TriInspectorMVCE;
using System.Reflection;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using UnityEngine.XR.Interaction.Toolkit;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
#if MV_UNITY_AI_NAV
using Unity.AI.Navigation;
#endif

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
        }

        private const string ThisProperty = "_this";
        private const string GameObjectProperty = "gameObject";
        private const string TransformProperty = "transform";
        private const string IsUnityNullFunctionOld = "isUnityNull";
        private const string IsUnityNullFunction = "NULL";
        private const string CoroutineFunction = "StartCoroutine";
        private const string GetMetaverseScriptFunction = "GetMetaverseScript";
        private const string PrintFunction = "print";
        private const string NewGuidFunction = "NewGuid";
        private const string GetGlobalFunction = "GetStaticReference";
        private const string SetGlobalFunction = "SetStaticReference";
        private const string MetaSpaceProperty = "MetaSpace";
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
        [HideInInspector][SerializeField] private Variables variables;

        private bool _ready;
        private Engine _engine;
        private Dictionary<ScriptFunctions, JsValue> _methods;
        private readonly Dictionary<string, JsValue> _functionLookup = new();
        private static int _timeoutHandleIndex;
        private readonly HashSet<int> _timeoutHandles = new();

        /// <summary>
        /// Gets the variable declarations for the javascript file.
        /// </summary>
        public VariableDeclarations Vars => variables ? variables.declarations : null;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (!Application.isPlaying)
            {
                if (variables == null || variables.gameObject == gameObject) 
                    return;
                DestroyImmediate(variables.gameObject);
                variables = null;
                return;
            }

            if (_methods != null && _ready)
            {
                if (_methods.TryGetValue(ScriptFunctions.OnDestroy, out var method))
                    _engine.Invoke(method);
            }

            _engine?.Dispose();
        }

        private void OnEnable()
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnEnable, out var method))
                _engine.Invoke(method);
        }

        private void OnDisable()
        {
            if (!_ready) return;
            if (_methods == null) return;
            if (_methods.TryGetValue(ScriptFunctions.OnDisable, out var method))
                _engine.Invoke(method);
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!InitializeEngine())
            {
                enabled = false;
                return;
            }

            if (MetaSpace.Instance)
            {
                MetaSpace.OnReady(() => OnReady());
                return;
            }

            OnReady();
            return;

            void OnReady()
            {
                MetaverseDispatcher.AtEndOfFrame(() => // At end of frame to ensure everything is initialized first.
                {
                    if (_methods.TryGetValue(ScriptFunctions.Awake, out var method))
                        _engine.Invoke(method);

                    if (enabled)
                    {
                        if (_methods.TryGetValue(ScriptFunctions.OnEnable, out method))
                            _engine.Invoke(method);
                    }
                    else
                    {
                        if (_methods.TryGetValue(ScriptFunctions.OnDisable, out method))
                            _engine.Invoke(method);
                    }

                    if (enabled && _methods.TryGetValue(ScriptFunctions.Start, out method))
                        _engine.Invoke(method);

                    _ready = true;
                });
            }
        }

#if UNITY_EDITOR
        
        private const string k_VariablesObjectName = "MetaverseScriptVariables";

        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
            if (Application.isPlaying)
                return;

            var variables = FindObjectsOfType<Variables>(true)
                .Where(x => x.name.StartsWith(k_VariablesObjectName) && x.transform.parent == null);

            var cleanedUpCount = 0;
            foreach (var orphanedMetaverseScript in variables)
            {
                cleanedUpCount++;
                DestroyImmediate(orphanedMetaverseScript.gameObject);
            }
            
            if (cleanedUpCount > 0)
                MetaverseProgram.Logger.Log($"Cleaned up {cleanedUpCount} orphaned Variables objects.");
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            
            if (Application.isPlaying)
                return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!this)
                    return;

                if (UnityEditor.EditorUtility.IsPersistent(gameObject))
                    return;

                var objectName = $"{k_VariablesObjectName}:\"{name}\"";
                if (!variables)
                {
                    var go = new GameObject(objectName);
                    variables = go.AddComponent<Variables>();
                }
                else if (variables.name != objectName)
                    variables.name = objectName;
                
                variables.transform.SetParent(transform);
            };
        }
#endif

        private void Update()
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.Update, out var method))
                _engine.Invoke(method);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.LateUpdate, out var method))
                _engine.Invoke(method);
        }

        private void FixedUpdate()
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.FixedUpdate, out var method))
                _engine.Invoke(method);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnTriggerEnter, out var method))
                _engine.Invoke(method, other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnTriggerExit, out var method))
                _engine.Invoke(method, other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnTriggerStay, out var method))
                _engine.Invoke(method, other);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnTriggerEnter2D, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnTriggerExit2D, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnTriggerStay2D, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnAnimatorIK(int layer)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnAnimatorIK, out var method))
                _engine.Invoke(method, layer);
        }

        private void OnAnimatorMove()
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnAnimatorMove, out var method))
                _engine.Invoke(method);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnCollisionEnter, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnCollisionExit, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnCollisionStay, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnCollisionEnter2D, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnCollisionExit2D, out var method))
                _engine.Invoke(method, collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!_ready) return;
            if (_methods.TryGetValue(ScriptFunctions.OnCollisionStay2D, out var method))
                _engine.Invoke(method, collision);
        }

        /// <summary>
        /// Executes a javascript function.
        /// </summary>
        /// <param name="fn">The function to execute.</param>
        public void ExecuteFunction(string fn)
        {
            if (string.IsNullOrEmpty(fn))
                return;

            if (!_ready)
            {
                MetaverseProgram.Logger.Log("The MetaSpace has not fully initialized yet. Call to '" + fn + "' ignored.");
                return;
            }

            if (!_functionLookup.TryGetValue(fn, out var method))
                _functionLookup[fn] = method = _engine.GetValue(fn);

            if (method != null && !method.IsUndefined())
                _engine.Invoke(method);
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
        /// Gets a property with the given name from the javascript engine.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The property value.</returns>
        public JsValue GetProperty(string propertyName)
        {
            return _engine?.GetValue(propertyName);
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

            _engine.SetValue(propertyName, value);
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

        private static void ApplyStaticEngineFunctions(Engine engine)
        {
            engine
                .SetValue(GetGlobalFunction, (Func<string, object>)(key => MetaverseScriptCache.Current.GetStaticReference(key))).SetValue(SetGlobalFunction, (Action<string, object>)((key, value) => MetaverseScriptCache.Current.SetStaticReference(key, value)))
                .SetValue(IsUnityNullFunctionOld, (Func<object, bool>)(o => o.IsUnityNull()))
                .SetValue(IsUnityNullFunction, (Func<object, bool>)(o => o.IsUnityNull()))
                .SetValue(PrintFunction, (Action<object>)(o => MetaverseProgram.Logger.Log(o)))
                .SetValue(NewGuidFunction, (Func<string>)(() => Guid.NewGuid().ToString()))
                .SetValue(MetaSpaceProperty, (object)MetaSpace.Instance)
                .SetValue(GetMetaverseScriptFunction, (Func<string, GameObject, object>)((n, go) => go.GetComponents<MetaverseScript>().FirstOrDefault(x => x.javascriptFile && x.javascriptFile.name == n)));
        }
        
        private bool InitializeEngine()
        {
            if (!javascriptFile)
                return false;

            _engine = new Engine(o => DefaultEngineOptions(o, true))

                .SetValue(ThisProperty, (object)gameObject)
                .SetValue(GameObjectProperty, (object)gameObject)
                .SetValue(TransformProperty, (object)transform)
                .SetValue(CoroutineFunction, (Action<Func<object>>)(o => StartCoroutine(CoroutineUpdate(o))))
                .SetValue(GetEnabledFunction, (Func<bool>)(() => enabled))
                .SetValue(nameof(Vars), Vars)
                .SetValue(SetEnabledFunction, (Action<bool>)(b => enabled = b))
                .SetValue(nameof(GetVar), (Func<string, object>)GetVar)
                .SetValue(nameof(TryGetVar), (Func<string, object, object>)TryGetVar)
                .SetValue(nameof(SetVar), (Action<string, object>)SetVar)
                .SetValue(SetTimeoutFunction, (Func<Action, int, int>)((action, time) =>
                {
                    var timeoutHandle = ++_timeoutHandleIndex;
                    _timeoutHandles.Add(timeoutHandle);
                    MetaverseDispatcher.WaitForSeconds(time / 1000f, () =>
                    {
                        if (!_timeoutHandles.Remove(timeoutHandle)) return;
                        if (!this) return;
                        action?.Invoke();
                    });
                    return timeoutHandle;
                }))
                .SetValue(ClearTimeoutFunction, (Action<int>)(handle => { _timeoutHandles.Remove(handle); }))
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

                            if (parent)
                                spawned.Transform.parent = parent;

                            spawned.Transform.localPosition = spawnPos;
                            spawned.Transform.localRotation = spawnRot;

                            callback?.Invoke(spawned.GameObject);
                            
                        }, pos, rot, false);
                    }))
                    .SetValue(AwaitFunction, (Action<object, Action<object>>)((t, action) =>
                    {
                        if (t is not Task task)
                        {
                            if (t is IEnumerator e)
                            {
                                e.ToUniTask().ContinueWith(() =>
                                {
                                    action?.Invoke(t);
                                });
                            }
                            
                            return;
                        }
                        
                        if (task.GetType().GenericTypeArguments.Length == 0)
                        {
                            task.AsUniTask().ContinueWith(() => action);
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
                        
                        continueWith.Invoke(uniTask, new [] { uniTask, action });
                    }));
            
            ApplyStaticEngineFunctions(_engine);

            _engine.Execute(MetaverseScriptCache.Current.GetScript(javascriptFile));

            var methods = (ScriptFunctions[])Enum.GetValues(typeof(ScriptFunctions));
            foreach (var method in methods)
                CacheMethod(method);

            return _methods != null;
        }

        private static void DefaultEngineOptions(Options options, bool strict)
        {
            options.AllowClr(
                    typeof(DateTime).Assembly,
                    typeof(Transform).Assembly,
                    typeof(GameObject).Assembly,
                    typeof(Component).Assembly, /* UnityEngine.CoreModule */
                    typeof(Rigidbody).Assembly, /* UnityEngine.PhysicsModule */
                    typeof(Terrain).Assembly, /* UnityEngine.TerrainModule */
                    typeof(AudioSource).Assembly, /* UnityEngine.AudioModule */
                    typeof(Canvas).Assembly, /* UnityEngine.UIModule */
                    typeof(NavMesh).Assembly, /* UnityEngine.AIModule */
                    typeof(NavMeshAgent).Assembly, /* UnityEngine.AIModule */
#if MV_UNITY_AI_NAV
                    typeof(NavMeshSurface).Assembly, /* Unity.AI.Navigation */
#endif
                    typeof(Input).Assembly, /* UnityEngine.InputModule */
                    typeof(MetaverseProgram).Assembly /* MetaverseCloudEngine */,
                    typeof(MetaverseClient).Assembly /* MetaverseCloudEngine.ApiClient */,
                    typeof(MetaSpaceDto).Assembly /* MetaverseCloudEngine.Common */,
                    typeof(TextMeshPro).Assembly /* TextMeshPro */,
                    typeof(InputSystem).Assembly /* New Input System */,
                    typeof(CinemachineCore).Assembly /* Cinema-chine */,
                    typeof(Variables).Assembly /* Visual Scripting */,
                    typeof(ActionBasedController).Assembly /* XR Interaction Toolkit */,
                    typeof(Task).Assembly /* System.Threading.Tasks */,
                    typeof(UniTask).Assembly, /* UniTask */
                    typeof(UniTaskExtensions).Assembly /* UniTask */
#if MV_PTC_VUFORIA && !UNITY_WEBGL && !UNITY_STANDALONE_LINUX
                    ,
                    typeof(Vuforia.VuforiaApplication).Assembly,
                    typeof(Vuforia.VuforiaConfiguration).Assembly
#endif
#if MV_UNITY_AR_FOUNDATION && (UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR)
                    ,
                    typeof(UnityEngine.XR.ARSubsystems.XRRaycastHit).Assembly,
                    typeof(UnityEngine.XR.ARFoundation.ARRaycastHit).Assembly
#endif
#if MV_UNITY_AR_CORE && (UNITY_ANDROID || UNITY_EDITOR)
                    ,
                    typeof(UnityEngine.XR.ARCore.ARCoreSessionSubsystem).Assembly
#endif
#if MV_UNITY_AR_CORE && MV_AR_CORE_EXTENSIONS && (UNITY_ANDROID || UNITY_EDITOR)
                    ,
                    typeof(Google.XR.ARCoreExtensions.ARAnchorManagerExtensions).Assembly
#endif
                    )
                .AllowClrWrite()
                .AllowOperatorOverloading()
                .SetTypeResolver(new Jint.Runtime.Interop.TypeResolver
                {
                    MemberFilter = IsMemberAllowed
                })
                .AddExtensionMethods(typeof(Enumerable), typeof(MVUtils), typeof(MetaverseDispatcherExtensions), typeof(UniTaskExtensions)
#if MV_AR_CORE_EXTENSIONS && (UNITY_ANDROID || UNITY_EDITOR) 
                    ,typeof(Google.XR.ARCoreExtensions.ARAnchorManagerExtensions)
#endif
                )
                .CatchClrExceptions();

            if (strict)
                options.Strict();

            options.Interop.TrackObjectWrapperIdentity = false;
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

            _methods ??= new Dictionary<ScriptFunctions, JsValue>();

            var val = _engine.GetValue(method.ToString());
            if (val.IsUndefined())
                return;

            _methods.Add(method, val);
        }
    }
}
