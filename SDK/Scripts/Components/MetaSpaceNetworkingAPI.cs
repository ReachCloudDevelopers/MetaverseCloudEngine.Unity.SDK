using MetaverseCloudEngine.Unity.Networking.Abstract;
using System;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [DeclareFoldoutGroup("Instance")]
    [DeclareFoldoutGroup("Players")]
    [HideMonoScript]
    public partial class MetaSpaceNetworkingAPI : MetaSpaceBehaviour
    {
        public UnityEvent onNetworkReady;
        [Group("Instance")] public UnityEvent<string> onInstanceName;
        [Group("Instance")] public UnityEvent<string> onInstanceUniqueID;
        [Group("Players")] public UnityEvent<int> onPlayerJoined;
        [Group("Players")] public UnityEvent<int> onPlayerLeft;
        [Group("Players")] public UnityEvent<int> onPlayerCountChanged;

        private IMetaSpaceNetworkingService _networking;
        
        protected override void OnMetaSpaceServicesRegistered()
        {
            _networking = MetaSpace.GetService<IMetaSpaceNetworkingService>();
            
            onPlayerCountChanged?.Invoke(_networking.PlayerCount);

            if (_networking.IsReady)
                OnNetworkReady();

            _networking.Ready += OnNetworkReady;
            _networking.PlayerJoined += OnPlayerJoined;
            _networking.PlayerLeft += OnPlayerLeft;
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            if (MetaSpace.RegisteredServices)
            {
                _networking.Ready -= OnNetworkReady;
                _networking.PlayerJoined -= OnPlayerJoined;
                _networking.PlayerLeft -= OnPlayerLeft;
            }
        }

        private void OnPlayerJoined(int playerId)
        {
            onPlayerJoined?.Invoke(playerId);
            onPlayerCountChanged?.Invoke(_networking.PlayerCount);
        }

        private void OnPlayerLeft(int playerId)
        {
            onPlayerLeft?.Invoke(playerId);
            onPlayerCountChanged?.Invoke(_networking.PlayerCount);
        }

        private void OnNetworkReady()
        {
            onNetworkReady?.Invoke();
            onInstanceName?.Invoke(_networking.InstanceID);

            string id = string.Empty;
            GetUniqueInstanceID(ref id);
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString();

            onInstanceUniqueID?.Invoke(id);
            onPlayerCountChanged?.Invoke(_networking.PlayerCount);
        }

        partial void GetUniqueInstanceID(ref string id);
    }
}