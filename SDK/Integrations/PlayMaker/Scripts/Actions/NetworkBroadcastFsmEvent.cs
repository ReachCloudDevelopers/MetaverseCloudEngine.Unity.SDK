#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Enumerations;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [ActionCategory(MetaverseConstants.ProductName)]
    public class NetworkBroadcastFsmEvent : FsmStateAction
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void GlobalInit()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    OnStarted(SceneManager.GetSceneAt(i));
                SceneManager.sceneLoaded += OnSceneLoaded;
            });
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            OnStarted(scene);
        }

        private static void OnStarted(Scene scene)
        {
            if (MetaSpace.Instance is not null && MetaSpace.Instance.gameObject.scene == scene)
                MetaSpace.OnReady(ms =>
                {
                    var mns = ms.GetService<IMetaSpaceNetworkingService>();
                    mns.AddEventHandler((short)NetworkEventType.PlayMakerRPC, OnPlayMakerEvent);
                });
        }

        private static void OnPlayMakerEvent(short eventId, int sendingPlayerID, object content)
        {
            if ((short)NetworkEventType.PlayMakerRPC != eventId) return;
            PlayMakerFSM.BroadcastEvent(content as string);
        }

        [HideIf(nameof(HideEventTarget))]
        public FsmEventTarget eventTarget = new () { target = FsmEventTarget.EventTarget.BroadcastAll };
        [RequiredField]
        public FsmEvent sendEvent;

        public NetworkMessageReceivers receivers = NetworkMessageReceivers.All;
        public bool buffered;

        public bool HideEventTarget() => true;

        public override void OnEnter()
        {
            MetaSpace.OnReady(ms =>
            {
                MetaverseDispatcher.AtEndOfFrame(() =>
                {
                    var mns = ms.GetService<IMetaSpaceNetworkingService>();
                    mns?.InvokeEvent((short)NetworkEventType.PlayMakerRPC, receivers, buffered, sendEvent.Name);
                    Finish();
                });
            });
        }
    }
}

#endif