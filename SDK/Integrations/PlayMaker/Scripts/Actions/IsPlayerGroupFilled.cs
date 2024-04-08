#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class IsPlayerGroupFilled : FsmStateAction
    {
        public FsmString identifier;
        [UIHint(UIHint.Variable)]
        public FsmBool storeFilled;
        public FsmEvent onFilled;
        public FsmEvent onNotFilled;

        public override void OnEnter()
        {
            MetaSpace.OnReady(space =>
            {
                var networkingSerivce = space.GetService<IMetaSpaceNetworkingService>();
                var playerGroupService = space.GetService<IPlayerGroupsService>();
                if (networkingSerivce == null || playerGroupService == null)
                    return;

                var filled = storeFilled.Value = playerGroupService.IsPlayerGroupFull(identifier.Value);
                if (filled)
                    Fsm.Event(onFilled);
                else
                    Fsm.Event(onNotFilled);

                Finish();
            });
        }
    }
}
#endif