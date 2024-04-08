using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Common.Enumerations;

namespace MetaverseCloudEngine.Unity.Components
{
    public partial class MetaSpacePortal : MonoBehaviour
    {
        [MetaSpaceIdProperty]
        [SerializeField] private string metaSpace;
        [SerializeField] private string instanceID;

        [Header("Visibility")]
        [Tooltip("If true, the instance being joined will be unlisted.")]
        [SerializeField] private bool unlisted;
        [Tooltip("If true, the instance being joined will be private and can't be joined by other players.")]
        [SerializeField] private bool @private;

        [Header("Spawn Points")]
        [FormerlySerializedAs("spawnPointIdentifier")]
        [SerializeField] private string spawnPointID;

        [Header("Blockchain")]
        [SerializeField] private string blockchainAsset;
        [SerializeField] private BlockchainType blockchainType = BlockchainType.Cardano;

        [Header("Events")]
        public UnityEvent onStartedLoading;
        public UnityEvent<string> onLoadingFailed;
        
        public bool IsJoining { get; private set; }

        public string InstanceID
        {
            get => instanceID;
            set => instanceID = value;
        }

        public string BlockchainAsset
        {
            get => blockchainAsset;
            set => blockchainAsset = value;
        }

        public BlockchainType BlockchainType {
            get => blockchainType;
            set => blockchainType = value;
        }

        public string MetaSpace
        {
            get => metaSpace;
            set => metaSpace = value;
        }

        public string SpawnPointID
        {
            get => spawnPointID;
            set => spawnPointID = value;
        }

        public bool Unlisted
        {
            get => unlisted;
            set => unlisted = value;
        }

        public bool Private 
        {
            get => @private;
            set => @private = value;
        }

        public void Join()
        {
            if (IsJoining)
                return;
            
            onStartedLoading?.Invoke();
            IsJoining = true;
            
            if (!Guid.TryParse(metaSpace, out Guid metaSpaceId))
            {
                JoinFailed("Meta Space ID is invalid.");
                return;
            }
            
            MetaverseProgram.ApiClient.MetaSpaces.FindAsync(metaSpaceId)
                .ResponseThen(space =>
                {
                    bool implemented = false;
                    JoinInternal(ref implemented, space);
#if UNITY_EDITOR
                    if (!implemented)
                    {
                        UnityEditor.EditorApplication.isPlaying = false;
                        MetaverseProgram.Logger.Log($"Meta Space '{space.Name}' with instance ID '{GetMetaSpaceInstanceID()}' would have been joined.");
                    }
#endif
                }, JoinFailed);
        }

        private void JoinFailed(object error)
        {
            IsJoining = false;
            onLoadingFailed?.Invoke(error.ToString());
        }

        partial void JoinInternal(ref bool isImplemented, MetaSpaceDto metaSpace);

        private string GetMetaSpaceInstanceID()
        {
            string propertyDefAppend = null;
            MetaSpacePortalInstancePropertyDefinition[] propertyDefComponents = GetPropertyDefinitions();
            if (propertyDefComponents.Length > 0)
            {
                string[] appendComponents = propertyDefComponents.Where(x => x.appendToInstanceID && !string.IsNullOrWhiteSpace(x.propertyName)).Select(x => x.propertyName + ":" + x.GetObjectValue()).ToArray();
                if (appendComponents.Length > 0)
                    propertyDefAppend = "_" + string.Join("_", appendComponents);
            }

            string finalInstanceID = instanceID?.Trim() + propertyDefAppend;
            return finalInstanceID;
        }

        private MetaSpacePortalInstancePropertyDefinition[] GetPropertyDefinitions()
        {
            if (!gameObject) return Array.Empty<MetaSpacePortalInstancePropertyDefinition>();
            return gameObject.GetTopLevelComponentsInChildrenOrdered<MetaSpacePortalInstancePropertyDefinition, MetaSpacePortal>();
        }
    }
}