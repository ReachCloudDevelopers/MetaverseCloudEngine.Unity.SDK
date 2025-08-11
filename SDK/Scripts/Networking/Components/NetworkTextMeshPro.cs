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
        [SerializeField, Attributes.ReadOnly] private string id;
        [SerializeField] private TMP_Text text;

        private string _lastTextValue;

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

            if (!NetworkObject.IsStateAuthority)
                return;

            if (_lastTextValue != text.text)
            {
                _lastTextValue = text.text;
                NetworkObject.InvokeRPC((short)NetworkRpcType.TextMeshProTextUpdate, NetworkMessageReceivers.Others, new object[] { text.text ?? string.Empty, ID });
            }
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
            if (content is not object[] args || args.Length != 2)
                return;
            Guid? id = args[1] as Guid?;
            if (id == null || id.Value != ID)
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