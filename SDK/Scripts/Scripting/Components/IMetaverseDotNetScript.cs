using System;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Services.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Interface implemented by runtime C# scripts that are executed via DotNow.
    /// </summary>
    public interface IMetaverseDotNetScript
    {
        /// <summary>
        /// Called once when the script instance is created.
        /// </summary>
        /// <param name="context">Provides access to the host component, GameObject and MetaSpace services.</param>
        void OnInitialize(MetaverseDotNetScriptContext context);

        // MetaSpace lifecycle
        void OnMetaSpaceBehaviourInitialize();

        void OnMetaSpaceServicesRegistered();

        void OnMetaSpaceBehaviourDestroyed();

        // Unity standard lifecycle
        void OnAwake();

        void OnOnEnable();

        void OnStart();

        void OnOnDisable();

        void OnUpdate();

        void OnLateUpdate();

        void OnFixedUpdate();

        void OnOnDestroy();

        void OnOnGUI();

        // 3D trigger events
        void OnOnTriggerEnter(Collider other);

        void OnOnTriggerExit(Collider other);

        void OnOnTriggerStay(Collider other);

        // 2D trigger events
        void OnOnTriggerEnter2D(Collider2D collision);

        void OnOnTriggerExit2D(Collider2D collision);

        void OnOnTriggerStay2D(Collider2D collision);

        // Animator events
        void OnOnAnimatorIK(int layerIndex);

        void OnOnAnimatorMove();

        // 3D collision events
        void OnOnCollisionEnter(Collision collision);

        void OnOnCollisionExit(Collision collision);

        void OnOnCollisionStay(Collision collision);

        // 2D collision events
        void OnOnCollisionEnter2D(Collision2D collision);

        void OnOnCollisionExit2D(Collision2D collision);

        void OnOnCollisionStay2D(Collision2D collision);
    }

    /// <summary>
    /// Convenience base class for C# scripts executed via DotNow. All methods are optional.
    /// </summary>
    public abstract class MetaverseDotNetScriptBase : IMetaverseDotNetScript
    {
        protected MetaverseDotNetScriptContext Context { get; private set; }

        // Expose Unity-style convenience properties as public so the DotNow runtime can
        // resolve them via reflection (it only searches for public methods when
        // resolving MemberReferences like get_transform / get_gameObject).
        public GameObject gameObject => Context?.GameObject;

        public Transform transform => Context?.Transform;

        public MetaSpace MetaSpace => Context?.MetaSpace;

        public virtual void OnInitialize(MetaverseDotNetScriptContext context)
        {
            Context = context;
        }

        public virtual void OnMetaSpaceBehaviourInitialize() { }

        public virtual void OnMetaSpaceServicesRegistered() { }

        public virtual void OnMetaSpaceBehaviourDestroyed() { }

        public virtual void OnAwake() { }

        public virtual void OnOnEnable() { }

        public virtual void OnStart() { }

        public virtual void OnOnDisable() { }

        public virtual void OnUpdate() { }

        public virtual void OnLateUpdate() { }

        public virtual void OnFixedUpdate() { }

        public virtual void OnOnDestroy() { }

        public virtual void OnOnGUI() { }

        public virtual void OnOnTriggerEnter(Collider other) { }

        public virtual void OnOnTriggerExit(Collider other) { }

        public virtual void OnOnTriggerStay(Collider other) { }

        public virtual void OnOnTriggerEnter2D(Collider2D collision) { }

        public virtual void OnOnTriggerExit2D(Collider2D collision) { }

        public virtual void OnOnTriggerStay2D(Collider2D collision) { }

        public virtual void OnOnAnimatorIK(int layerIndex) { }

        public virtual void OnOnAnimatorMove() { }

        public virtual void OnOnCollisionEnter(Collision collision) { }

        public virtual void OnOnCollisionExit(Collision collision) { }

        public virtual void OnOnCollisionStay(Collision collision) { }

        public virtual void OnOnCollisionEnter2D(Collision2D collision) { }

        public virtual void OnOnCollisionExit2D(Collision2D collision) { }

        public virtual void OnOnCollisionStay2D(Collision2D collision) { }
    }

    /// <summary>
    /// Provides access to the hosting MetaverseDotNetScript component and MetaSpace services.
    /// </summary>
    public sealed class MetaverseDotNetScriptContext
    {
        public MetaverseDotNetScript Host { get; }

        public GameObject GameObject => Host ? Host.gameObject : null;

        public Transform Transform => Host ? Host.transform : null;

        public MetaSpace MetaSpace { get; }

        public MetaverseDotNetScriptContext(MetaverseDotNetScript host, MetaSpace metaSpace)
        {
            Host = host;
            MetaSpace = metaSpace;
        }

        /// <summary>
        /// Resolves a MetaSpace service of the given type.
        /// </summary>
        public T GetService<T>() where T : class, IMetaSpaceService
        {
            if (!MetaSpace)
                return null;
            return MetaSpace.GetService<T>();
        }
    }
}

