#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class SetLocalPlayerGroup : FsmStateAction
    {
        public FsmString identifier;
        [UIHint(UIHint.Variable)]
        public FsmBool storeSuccess;
        public FsmEvent onSuccess;
        public FsmEvent onFailed;

        public override void OnEnter()
        {
            MetaSpace.OnReady(space =>
            {
                var networkingSerivce = space.GetService<IMetaSpaceNetworkingService>();
                var playerGroupService = space.GetService<IPlayerGroupsService>();
                if (networkingSerivce == null || playerGroupService == null)
                    return;

                var success = storeSuccess.Value = playerGroupService.TrySetPlayerPlayerGroup(networkingSerivce.LocalPlayerID, identifier.Value);
                if (success)
                    Fsm.Event(onSuccess);
                else
                    Fsm.Event(onFailed);

                Finish();
            });
        }
    }
}
#endif