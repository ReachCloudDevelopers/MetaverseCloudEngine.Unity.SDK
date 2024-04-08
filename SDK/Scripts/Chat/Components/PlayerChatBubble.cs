using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Chat.Components
{
    /// <summary>
    /// A chat bubble that is displayed above the player's head.
    /// </summary>
    [DisallowMultipleComponent]
    [Experimental]
    [HideMonoScript]
    public partial class PlayerChatBubble : NetworkObjectBehaviour
    {
        [Tooltip("Invoked when a chat message by the owning client is sent.")]
        public UnityEvent<string> onChatMessage; 
    }
}
