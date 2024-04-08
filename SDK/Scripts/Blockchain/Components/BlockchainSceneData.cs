using System;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Blockchain.Components
{
    [DefaultExecutionOrder(ExecutionOrder.PostInitialization + 1)]
    [Experimental]
    [HideMonoScript]
    public partial class BlockchainSceneData : MetaSpaceBehaviour
    {
        [Serializable]
        public class BlockchainSceneDataEvents
        {
            public UnityEvent onBlockchainSceneDataPresent;
            public UnityEvent onBlockchainSceneDataNotPresent;
            
            public BlockchainType blockchainType = BlockchainType.Cardano;
            public UnityEvent<string> onBlockchainAssetPresent;
        }

        public BlockchainSceneDataEvents sceneDataEvents = new ();
        public BlockchainMetaData tokenMetaData;

        public bool IsLoading { get; private set; }

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            if (MetaSpace.GetService<IMetaSpaceNetworkingService>().IsOfflineMode)
            {
                if (!Application.isEditor)
                    MetaverseProgram.Logger.Log("Offline Mode Detected... Ignoring Blockchain Query");
                sceneDataEvents.onBlockchainSceneDataNotPresent?.Invoke(); // TODO FIXME: Simulate somehow?
                return;
            }

            CheckBlockchainDataInternal();
        }

        partial void CheckBlockchainDataInternal();
    }
}