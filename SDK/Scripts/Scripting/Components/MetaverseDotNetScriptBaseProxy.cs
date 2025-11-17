#if MV_DOTNOW_SCRIPTING

using System;
using System.Reflection;
using dotnow;
using dotnow.Interop;
using UnityEngine;
using AppDomain = dotnow.AppDomain;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// DotNow proxy binding for MetaverseDotNetScriptBase so interpreted scripts
    /// that inherit from this base can be instantiated and invoked via DotNow.
    /// </summary>
    [CLRProxyBinding(typeof(MetaverseDotNetScriptBase))]
    internal class MetaverseDotNetScriptBaseProxy : MetaverseDotNetScriptBase, ICLRProxy
    {
        private ICLRInstance _instance;

        // Cached method infos for all lifecycle methods
        private MethodBase _onInitialize;
        private MethodBase _onMetaSpaceBehaviourInitialize;
        private MethodBase _onMetaSpaceServicesRegistered;
        private MethodBase _onMetaSpaceBehaviourDestroyed;
        private MethodBase _onAwake;
        private MethodBase _onOnEnable;
        private MethodBase _onStart;
        private MethodBase _onOnDisable;
        private MethodBase _onUpdate;
        private MethodBase _onLateUpdate;
        private MethodBase _onFixedUpdate;
        private MethodBase _onOnDestroy;
        private MethodBase _onOnGUI;
        private MethodBase _onOnTriggerEnter;
        private MethodBase _onOnTriggerExit;
        private MethodBase _onOnTriggerStay;
        private MethodBase _onOnTriggerEnter2D;
        private MethodBase _onOnTriggerExit2D;
        private MethodBase _onOnTriggerStay2D;
        private MethodBase _onOnAnimatorIK;
        private MethodBase _onOnAnimatorMove;
        private MethodBase _onOnCollisionEnter;
        private MethodBase _onOnCollisionExit;
        private MethodBase _onOnCollisionStay;
        private MethodBase _onOnCollisionEnter2D;
        private MethodBase _onOnCollisionExit2D;
        private MethodBase _onOnCollisionStay2D;

        public ICLRInstance Instance => _instance;

        public void Initialize(AppDomain appDomain, Type type, ICLRInstance instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            _onInitialize = type.GetMethod(nameof(OnInitialize), Flags);
            _onMetaSpaceBehaviourInitialize = type.GetMethod(nameof(OnMetaSpaceBehaviourInitialize), Flags);
            _onMetaSpaceServicesRegistered = type.GetMethod(nameof(OnMetaSpaceServicesRegistered), Flags);
            _onMetaSpaceBehaviourDestroyed = type.GetMethod(nameof(OnMetaSpaceBehaviourDestroyed), Flags);
            _onAwake = type.GetMethod(nameof(OnAwake), Flags);
            _onOnEnable = type.GetMethod(nameof(OnOnEnable), Flags);
            _onStart = type.GetMethod(nameof(OnStart), Flags);
            _onOnDisable = type.GetMethod(nameof(OnOnDisable), Flags);
            _onUpdate = type.GetMethod(nameof(OnUpdate), Flags);
            _onLateUpdate = type.GetMethod(nameof(OnLateUpdate), Flags);
            _onFixedUpdate = type.GetMethod(nameof(OnFixedUpdate), Flags);
            _onOnDestroy = type.GetMethod(nameof(OnOnDestroy), Flags);
            _onOnGUI = type.GetMethod(nameof(OnOnGUI), Flags);
            _onOnTriggerEnter = type.GetMethod(nameof(OnOnTriggerEnter), Flags);
            _onOnTriggerExit = type.GetMethod(nameof(OnOnTriggerExit), Flags);
            _onOnTriggerStay = type.GetMethod(nameof(OnOnTriggerStay), Flags);
            _onOnTriggerEnter2D = type.GetMethod(nameof(OnOnTriggerEnter2D), Flags);
            _onOnTriggerExit2D = type.GetMethod(nameof(OnOnTriggerExit2D), Flags);
            _onOnTriggerStay2D = type.GetMethod(nameof(OnOnTriggerStay2D), Flags);
            _onOnAnimatorIK = type.GetMethod(nameof(OnOnAnimatorIK), Flags);
            _onOnAnimatorMove = type.GetMethod(nameof(OnOnAnimatorMove), Flags);
            _onOnCollisionEnter = type.GetMethod(nameof(OnOnCollisionEnter), Flags);
            _onOnCollisionExit = type.GetMethod(nameof(OnOnCollisionExit), Flags);
            _onOnCollisionStay = type.GetMethod(nameof(OnOnCollisionStay), Flags);
            _onOnCollisionEnter2D = type.GetMethod(nameof(OnOnCollisionEnter2D), Flags);
            _onOnCollisionExit2D = type.GetMethod(nameof(OnOnCollisionExit2D), Flags);
            _onOnCollisionStay2D = type.GetMethod(nameof(OnOnCollisionStay2D), Flags);

            _ = appDomain;
        }

        private static void InvokeVoid(MethodBase method, ICLRInstance instance, object[] args, string methodDisplayName)
        {
            if (method == null || instance == null)
                return;

            try
            {
                method.Invoke(instance, args);
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"[METAVERSE_DOTNET_SCRIPT] Exception in script {methodDisplayName}: {ex}");
            }
        }

        public override void OnInitialize(MetaverseDotNetScriptContext context)
        {
            // Ensure the proxy itself has a valid Context so that interop calls from DotNow
            // to convenience properties like transform / gameObject work correctly.
            base.OnInitialize(context);

            // Forward the call into the interpreted script instance so any user-defined
            // OnInitialize override still runs as expected.
            InvokeVoid(_onInitialize, _instance, new object[] { context }, nameof(OnInitialize));
        }

        public override void OnMetaSpaceBehaviourInitialize()
        {
            InvokeVoid(_onMetaSpaceBehaviourInitialize, _instance, null, nameof(OnMetaSpaceBehaviourInitialize));
        }

        public override void OnMetaSpaceServicesRegistered()
        {
            InvokeVoid(_onMetaSpaceServicesRegistered, _instance, null, nameof(OnMetaSpaceServicesRegistered));
        }

        public override void OnMetaSpaceBehaviourDestroyed()
        {
            InvokeVoid(_onMetaSpaceBehaviourDestroyed, _instance, null, nameof(OnMetaSpaceBehaviourDestroyed));
        }

        public override void OnAwake()
        {
            InvokeVoid(_onAwake, _instance, null, nameof(OnAwake));
        }

        public override void OnOnEnable()
        {
            InvokeVoid(_onOnEnable, _instance, null, nameof(OnOnEnable));
        }

        public override void OnStart()
        {
            InvokeVoid(_onStart, _instance, null, nameof(OnStart));
        }

        public override void OnOnDisable()
        {
            InvokeVoid(_onOnDisable, _instance, null, nameof(OnOnDisable));
        }

        public override void OnUpdate()
        {
            InvokeVoid(_onUpdate, _instance, null, nameof(OnUpdate));
        }

        public override void OnLateUpdate()
        {
            InvokeVoid(_onLateUpdate, _instance, null, nameof(OnLateUpdate));
        }

        public override void OnFixedUpdate()
        {
            InvokeVoid(_onFixedUpdate, _instance, null, nameof(OnFixedUpdate));
        }

        public override void OnOnGUI()
        {
            InvokeVoid(_onOnGUI, _instance, null, nameof(OnOnGUI));
        }

        public override void OnOnTriggerEnter(Collider other)
        {
            InvokeVoid(_onOnTriggerEnter, _instance, new object[] { other }, nameof(OnOnTriggerEnter));
        }

        public override void OnOnTriggerExit(Collider other)
        {
            InvokeVoid(_onOnTriggerExit, _instance, new object[] { other }, nameof(OnOnTriggerExit));
        }

        public override void OnOnTriggerStay(Collider other)
        {
            InvokeVoid(_onOnTriggerStay, _instance, new object[] { other }, nameof(OnOnTriggerStay));
        }

        public override void OnOnTriggerEnter2D(Collider2D collision)
        {
            InvokeVoid(_onOnTriggerEnter2D, _instance, new object[] { collision }, nameof(OnOnTriggerEnter2D));
        }

        public override void OnOnTriggerExit2D(Collider2D collision)
        {
            InvokeVoid(_onOnTriggerExit2D, _instance, new object[] { collision }, nameof(OnOnTriggerExit2D));
        }

        public override void OnOnTriggerStay2D(Collider2D collision)
        {
            InvokeVoid(_onOnTriggerStay2D, _instance, new object[] { collision }, nameof(OnOnTriggerStay2D));
        }

        public override void OnOnAnimatorIK(int layerIndex)
        {
            InvokeVoid(_onOnAnimatorIK, _instance, new object[] { layerIndex }, nameof(OnOnAnimatorIK));
        }

        public override void OnOnAnimatorMove()
        {
            InvokeVoid(_onOnAnimatorMove, _instance, null, nameof(OnOnAnimatorMove));
        }

        public override void OnOnCollisionEnter(Collision collision)
        {
            InvokeVoid(_onOnCollisionEnter, _instance, new object[] { collision }, nameof(OnOnCollisionEnter));
        }

        public override void OnOnCollisionExit(Collision collision)
        {
            InvokeVoid(_onOnCollisionExit, _instance, new object[] { collision }, nameof(OnOnCollisionExit));
        }

        public override void OnOnCollisionStay(Collision collision)
        {
            InvokeVoid(_onOnCollisionStay, _instance, new object[] { collision }, nameof(OnOnCollisionStay));
        }

        public override void OnOnCollisionEnter2D(Collision2D collision)
        {
            InvokeVoid(_onOnCollisionEnter2D, _instance, new object[] { collision }, nameof(OnOnCollisionEnter2D));
        }

        public override void OnOnCollisionExit2D(Collision2D collision)
        {
            InvokeVoid(_onOnCollisionExit2D, _instance, new object[] { collision }, nameof(OnOnCollisionExit2D));
        }

        public override void OnOnCollisionStay2D(Collision2D collision)
        {
            InvokeVoid(_onOnCollisionStay2D, _instance, new object[] { collision }, nameof(OnOnCollisionStay2D));
        }

        public override void OnOnDestroy()
        {
            InvokeVoid(_onOnDestroy, _instance, null, nameof(OnOnDestroy));
        }
    }
}


#endif // MV_DOTNOW_SCRIPTING
