#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class CheckLocalPlayerGroup : FsmStateAction
    {
        public FsmString identifier;
        [UIHint(UIHint.Variable)]
        public FsmBool storeInGroup;
        public FsmEvent onInGroup;
        public FsmEvent onNotInGroup;

        public override void OnEnter()
        {
            MetaSpace.OnReady(space =>
            {
                var networkingSerivce = space.GetService<IMetaSpaceNetworkingService>();
                var playerGroupService = space.GetService<IPlayerGroupsService>();
                if (networkingSerivce == null || playerGroupService == null) 
                    return;
                
                if (!playerGroupService.TryGetPlayerPlayerGroup(networkingSerivce.LocalPlayerID, out var playerGroup))
                    return;

                storeInGroup.Value = playerGroup.identifier == identifier.Value;
                Fsm.Event(playerGroup.identifier != identifier.Value ? onNotInGroup : onInGroup);
                Finish();
            });
        }
    }
}
#endif