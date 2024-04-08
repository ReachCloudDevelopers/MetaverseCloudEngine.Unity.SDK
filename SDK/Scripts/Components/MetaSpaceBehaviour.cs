using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A behaviour that reacts to the initialization and de-initialization of the meta space.
    /// </summary>
    [HideMonoScript]
    public abstract class MetaSpaceBehaviour : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The meta space that this behaviour exists in.
        /// </summary>
        protected MetaSpace MetaSpace;
        
        protected virtual void Awake()
        {
            TryInit();
        }

        protected virtual void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            if (MetaSpace)
            {
                MetaSpace.ServicesRegistered -= OnMetaSpaceServicesRegistered;
                MetaSpace.Initialized -= OnMetaSpaceBehaviourInitialize;
            }
            if (MetaSpace == null || (!MetaSpace.IsInitialized && !MetaSpace.RegisteredServices)) return;
            OnMetaSpaceBehaviourDestroyed();
        }
        
        private void TryInit()
        {
            MetaSpace = MetaSpace.Instance;
            if (MetaSpace == null)
            {
                // We may need to do this in case meta space
                // hasn't initialized yet.
                MetaSpace = FindObjectOfType<MetaSpace>();
                if (MetaSpace == null)
                    return;
            }

            if (!MetaSpace.RegisteredServices)
                MetaSpace.ServicesRegistered += OnMetaSpaceServicesRegistered;
            else
                OnMetaSpaceServicesRegistered();

            if (!MetaSpace.IsInitialized)
                MetaSpace.Initialized += OnMetaSpaceBehaviourInitialize;
            else 
                OnMetaSpaceBehaviourInitialize();
        }
        
        protected virtual void OnMetaSpaceBehaviourDestroyed()
        {
        }

        protected virtual void OnMetaSpaceServicesRegistered()
        {
        }

        protected virtual void OnMetaSpaceBehaviourInitialize()
        {
        }
    }
}