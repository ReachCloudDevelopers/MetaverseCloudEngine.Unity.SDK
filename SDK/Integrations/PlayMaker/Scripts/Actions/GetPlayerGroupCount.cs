﻿#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class GetPlayerGroupCount : FsmStateAction
    {
        public FsmString identifier;
        [UIHint(UIHint.Variable)]
        public FsmInt storeCount;

        public override void OnEnter()
        {
            MetaSpace.OnReady(space =>
            {
                var networkingSerivce = space.GetService<IMetaSpaceNetworkingService>();
                var playerGroupService = space.GetService<IPlayerGroupsService>();
                if (networkingSerivce == null || playerGroupService == null)
                    return;

                storeCount.Value = playerGroupService.GetPlayerGroupPlayerCount(identifier.Value);
                Finish();
            });
        }
    }
}
#endif