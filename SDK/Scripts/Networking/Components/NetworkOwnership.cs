using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecutionOrder.PreInitialization - 1)]
    [DeclareFoldoutGroup("Input Authority")]
    [DeclareFoldoutGroup("State Authority")]
    [HideMonoScript]
    public class NetworkOwnership : NetworkObjectBehaviour
    {
        [System.Serializable]
        public class ClientState
        {
            public List<Behaviour> behaviours;
            public List<GameObject> objects;
            public UnityEvent onBecome;
        }

        [InfoBox("The owner has input authority of this object (i.e. a Player).")]
        [Group("Input Authority")][LabelText("Local")] public ClientState localOwner;
        [Group("Input Authority")][LabelText("Remote")] public ClientState remoteOwner;

        [InfoBox("The controller has state authority of this object (i.e. Server / Host).")]
        [Group("State Authority")][LabelText("Local")] public ClientState localControl;
        [Group("State Authority")][LabelText("Remote")] public ClientState remoteControl;

        protected override void Awake()
        {
            base.Awake();

            if (!NetworkObject) return;
            if (NetworkObject.IsInitialized)
                return;

            if (!MetaSpace)
            {
                ToggleClientState(localControl, true);
                ToggleClientState(localOwner, true);
            }
            else
            {
                ToggleClientState(localControl, false);
                ToggleClientState(localOwner, false);
            }

            ToggleClientState(remoteControl, false);
            ToggleClientState(remoteOwner, false);
        }

        public override void OnLocalStateAuthority()
        {
            ToggleClientState(localControl, true);
            ToggleClientState(remoteControl, false);
        }

        public override void OnRemoteStateAuthority()
        {
            ToggleClientState(localControl, false);
            ToggleClientState(remoteControl, true);
        }

        public override void OnLocalInputAuthority()
        {
            ToggleClientState(localOwner, true);
            ToggleClientState(remoteOwner, false);
        }

        public override void OnRemoteInputAuthority()
        {
            ToggleClientState(localOwner, false);
            ToggleClientState(remoteOwner, true);
        }

        private static void ToggleClientState(ClientState state, bool isState)
        {
            foreach (var beh in state.behaviours.Where(beh => beh))
                beh.enabled = isState;

            foreach (var obj in state.objects.Where(obj => obj))
                obj.SetActive(isState);

            if (isState)
                state.onBecome?.Invoke();
        }
    }
}