#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class IsInPlayerGroup : FsmStateAction
    {
        public FsmString identifier;
        [UIHint(UIHint.Variable)]
        public FsmBool storeValue;
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

                var success = storeValue.Value = playerGroupService.IsInPlayerGroup(networkingSerivce.LocalPlayerID, identifier.Value);
                if (success)
                    Fsm.Event(onInGroup);
                else
                    Fsm.Event(onNotInGroup);

                Finish();
            });
        }
    }
}
#endif