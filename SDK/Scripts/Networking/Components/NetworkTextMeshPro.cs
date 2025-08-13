using MetaverseCloudEngine.Unity.Networking.Enumerations;
using System;
using System.Linq;
using TMPro;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [Experimental]
    [HideMonoScript]
    public class NetworkTextMeshPro : NetworkObjectBehaviour
    {
        [SerializeField]
        [Attributes.ReadOnly] private string id;
        [SerializeField] private TMP_Text text;

        private string _lastTextValue;
        private float _nextUpdateTime;

        private Guid? _id;
        private Guid ID {
            get {
                if (_id == null)
                {
                    Guid.TryParse(id, out Guid guid);
                    _id = guid;
                }
                return _id.GetValueOrDefault();
            }
        }

        private void EnsureUniqueRpcId()
        {
            if (!string.IsNullOrEmpty(id) && !IsRpcIdCopied())
                return;

            id = Guid.NewGuid().ToString();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureUniqueRpcId();
        }

        protected override void RegisterNetworkRPCs()
        {
            NetworkObject.RegisterRPC((short)NetworkRpcType.TextMeshProTextUpdate, RPC_OnTextMeshProTextUpdated, @override: false);
            NetworkObject.RegisterRPC((short)NetworkRpcType.TextMeshProTextRequest, RPC_OnTextMeshProTextRequested, @override: false);
        }

        protected override void UnRegisterNetworkRPCs()
        {
            NetworkObject.UnregisterRPC((short)NetworkRpcType.TextMeshProTextUpdate, RPC_OnTextMeshProTextUpdated);
            NetworkObject.UnregisterRPC((short)NetworkRpcType.TextMeshProTextRequest, RPC_OnTextMeshProTextRequested);
        }

        public override void OnRemoteStateAuthority()
        {
            NetworkObject.InvokeRPC((short)NetworkRpcType.TextMeshProTextRequest, NetworkObject.InputAuthorityID, null);
        }

        private void FixedUpdate()
        {
            if (!text)
            {
                MetaverseProgram.Logger.LogWarning("Text is not assigned.");
                enabled = false;
                return;
            }

            if (!NetworkObject.IsInputAuthority)
                return;

            if (_lastTextValue == text.text || Time.unscaledTime > _nextUpdateTime) return;
            _lastTextValue = text.text;
            _nextUpdateTime = Time.unscaledTime + 5;
            NetworkObject.InvokeRPC(
                (short)NetworkRpcType.TextMeshProTextUpdate, 
                NetworkMessageReceivers.Others, 
                new object[] { text.text ?? string.Empty, ID });
        }

        private void RPC_OnTextMeshProTextRequested(short procedureID, int playerID, object content)
        {
            NetworkObject.InvokeRPC((short)NetworkRpcType.TextMeshProTextUpdate, playerID, new object[]
            {
                text.text ?? string.Empty,
                ID
            });
        }

        private void RPC_OnTextMeshProTextUpdated(short procedureID, int playerID, object content)
        {
            if (content is not object[] { Length: 2 } args)
                return;
            if (args[1] is not Guid guid || guid != ID)
                return;
            if (args[0] is not string s)
                return;
            text.text = s;
        }

        private bool IsRpcIdCopied()
        {
            if (!NetworkObject) return false;
            return NetworkObject.gameObject
                .GetTopLevelComponentsInChildrenOrdered<NetworkTextMeshPro, NetworkObject>()
                .Any(x => x != this && x.id == id);
        }
    }
}