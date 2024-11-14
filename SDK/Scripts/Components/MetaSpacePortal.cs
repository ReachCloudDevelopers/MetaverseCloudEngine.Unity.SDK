using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public partial class MetaSpacePortal : TriInspectorMonoBehaviour
    {
        [MetaSpaceIdProperty]
        [InfoBox("Leaving Meta Space empty will default to the current Meta Space.")]
        [SerializeField] private string metaSpace;
        [SerializeField] private string instanceID;
        [FormerlySerializedAs("useInstanceID")]
        [SerializeField] private bool useCurrentInstanceID;
        [FormerlySerializedAs("organizationId")]
        [OrganizationIdProperty]
        [InfoBox("Make sure that the Organization has this metaspace added, otherwise joining will fail.")]
        [SerializeField] private string organization;
        [HideIf(nameof(organization))]
        [SerializeField] private bool defaultToCurrentOrganization = true;

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
        
        public string Organization
        {
            get => organization;
            set => organization = value;
        }
        
        public Guid? OrganizationId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(organization))
                    return null;
                return Guid.TryParse(organization, out var id) ? id : (Guid?) null;
            }
            set => organization = value?.ToString() ?? string.Empty;
        }

        public string InstanceID
        {
            get => instanceID;
            set => instanceID = value;
        }

        public bool UseInstanceID
        {
            get => useCurrentInstanceID;
            set => useCurrentInstanceID = value;
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

            if (string.IsNullOrWhiteSpace(metaSpace) && Assets.MetaSpaces.MetaSpace.Instance)
                metaSpace = Assets.MetaSpaces.MetaSpace.Instance.IDString;
            
            if (!Guid.TryParse(metaSpace, out var metaSpaceId))
            {
                JoinFailed("Meta Space ID is invalid.");
                return;
            }

            if (string.IsNullOrWhiteSpace(organization) && defaultToCurrentOrganization)
            {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                organization = MetaverseProgram.RuntimeServices.InternalOrganizationManager.SelectedOrganization?.Id.ToString();
#endif
            }

            if (!string.IsNullOrWhiteSpace(organization))
            {
                if (!Guid.TryParse(organization.Trim(), out var organizationId))
                {
                    JoinFailed("Organization ID is invalid.");
                    return;
                }

                MetaverseProgram.ApiClient.Organizations.GetAllMetaSpacesAsync(
                        new MetaSpaceQueryParams
                        {
                            OrganizationId = organizationId,
                            SpecificAssetId = metaSpaceId
                        })
                    .ResponseThen(r =>
                    {
                        var spaces = r as OrganizationMetaSpaceDto[] ?? r.ToArray();
                        if (!spaces.Any())
                        {
                            JoinFailed("Meta Space not found in organization.");
                            return;
                        }
                        OnMetaSpaceFound(spaces.First().MetaSpace);
                    }, JoinFailed);
                return;
            }
            
            MetaverseProgram.ApiClient.MetaSpaces.FindAsync(metaSpaceId)
                .ResponseThen(OnMetaSpaceFound, JoinFailed);
        }

        private void OnMetaSpaceFound(MetaSpaceDto space)
        {
            var implemented = false;
            JoinInternal(ref implemented, space);
#if UNITY_EDITOR
            if (implemented) return;
            UnityEditor.EditorApplication.isPlaying = false;
            MetaverseProgram.Logger.Log($"Meta Space '{space.Name}' with instance ID '{GetMetaSpaceInstanceID()}' would have been joined.");
#endif
        }

        private void JoinFailed(object error)
        {
            IsJoining = false;
            onLoadingFailed?.Invoke(error.ToString());
        }

        partial void JoinInternal(ref bool isImplemented, MetaSpaceDto metaSpace);

        private string GetMetaSpaceInstanceID()
        {
            if (useCurrentInstanceID)
            {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                var currentInstance = MetaverseProgram.RuntimeServices.InternalSceneManager.CurrentJoinState.InstanceID;
                if (!string.IsNullOrEmpty(currentInstance))
                    return currentInstance;
#endif
            }
            
            string propertyDefAppend = null;
            var propertyDefComponents = GetPropertyDefinitions();
            if (propertyDefComponents.Length > 0)
            {
                var appendComponents = propertyDefComponents.Where(x => x.appendToInstanceID && !string.IsNullOrWhiteSpace(x.propertyName)).Select(x => x.propertyName + ":" + x.GetObjectValue()).ToArray();
                if (appendComponents.Length > 0)
                    propertyDefAppend = "_" + string.Join("_", appendComponents);
            }

            var finalInstanceID = instanceID?.Trim() + propertyDefAppend;
            return finalInstanceID;
        }

        private MetaSpacePortalInstancePropertyDefinition[] GetPropertyDefinitions()
        {
            return !gameObject 
                ? Array.Empty<MetaSpacePortalInstancePropertyDefinition>() 
                : gameObject.GetTopLevelComponentsInChildrenOrdered<MetaSpacePortalInstancePropertyDefinition, MetaSpacePortal>();
        }
    }
}