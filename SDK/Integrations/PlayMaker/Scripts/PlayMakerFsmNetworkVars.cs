#if PLAYMAKER && METAVERSE_CLOUD_ENGINE

using System;
using System.Collections.Generic;

using HutongGames.PlayMaker;

using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;

using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker
{
    [HideMonoScript]
    public class PlayMakerFsmNetworkVars : NetworkObjectBehaviour
    {
        public bool useGlobals;
        [Required, TriInspectorMVCE.HideIf(nameof(useGlobals))] 
        public PlayMakerFSM fsm;
        public string[] variablesToSync = Array.Empty<string>();
        [Space]
        public UnityEvent onVariableChanged;

        private bool _isInitialSend = true;
        private readonly Dictionary<string, object> _variableChangedMap = new();

        private void Reset()
        {
            if (!fsm && !useGlobals) fsm = GetComponent<PlayMakerFSM>();
        }

        protected override void Awake()
        {
            if (!fsm && !useGlobals) fsm = GetComponent<PlayMakerFSM>();
            base.Awake();
        }

        private void FixedUpdate()
        {
            NetworkUpdate();
        }

        protected override void RegisterNetworkRPCs()
        {
            NetworkObject.RegisterRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesUpdate, RPC_PlayMakerFsmNetworkVariablesUpdate, @override: false);
            NetworkObject.RegisterRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesRequest, RPC_PlayMakerFsmNetworkVariablesRequest, @override: false);
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            NetworkObject.UnregisterRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesUpdate, RPC_PlayMakerFsmNetworkVariablesUpdate);
            NetworkObject.UnregisterRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesRequest, RPC_PlayMakerFsmNetworkVariablesRequest);
        }

        public override void OnNetworkReady(bool offline)
        {
            if (offline) return;
            if (!NetworkObject.IsStateAuthority)
                NetworkObject.InvokeRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesRequest, NetworkObject.StateAuthorityID, NetworkObject.Networking.LocalPlayerID);
        }

        private void NetworkUpdate()
        {
            if (!NetworkObject)
            {
                enabled = false;
                return;
            }

            if (GetVariables() == null)
            {
                enabled = false;
                return;
            }

            if (NetworkObject.IsInitialized && NetworkObject.IsStateAuthority)
                SendDirtyVariables();
        }

        public override void OnLocalStateAuthority()
        {
            if (NetworkObject.IsStateAuthority)
            {
                SendDirtyVariables(sendAll: _isInitialSend);
                _isInitialSend = false;
            }
        }

        public override void OnRemoteStateAuthority()
        {
            _isInitialSend = true;
            NetworkObject.InvokeRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesRequest, NetworkObject.StateAuthorityID, NetworkObject.Networking.LocalPlayerID);
        }

        private void SendDirtyVariables(bool sendAll = false, int? targetPlayer = null)
        {
            var isDirty = false;
            Dictionary<string, object> variablesToSend = null;
            for (var i = variablesToSync.Length - 1; i >= 0; i--)
            {
                var variableName = variablesToSync[i];
                var variable = GetVariables().GetVariable(variableName);
                if (variable == null)
                    continue;

                var variableValue = variable.RawValue;
                if (!sendAll && _variableChangedMap.TryGetValue(variableName, out var cachedValue) && cachedValue?.Equals(variableValue) == true)
                    continue;

                _variableChangedMap[variableName] = variableValue;
                variablesToSend ??= new Dictionary<string, object>();
                if (variableValue is not null && variableValue is not string)
                {
                    var variableType = variableValue.GetType();
                    if (!variableType.IsPrimitive)
                        continue; // skip all non primitive types
                }

                variablesToSend[variableName] = variableValue;
                isDirty = true;
            }

            if (!isDirty) return;
            if (targetPlayer != null) NetworkObject.InvokeRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesUpdate, targetPlayer.Value, variablesToSend);
            else NetworkObject.InvokeRPC((short)NetworkRpcType.PlayMakerFsmNetworkVariablesUpdate, NetworkMessageReceivers.Others, variablesToSend);

            try { onVariableChanged?.Invoke(); }
            catch { /* ignored */ }
        }

        private void RPC_PlayMakerFsmNetworkVariablesRequest(short procedureId, int playerID, object content)
        {
            SendDirtyVariables(true, (int)content);
        }

        private void RPC_PlayMakerFsmNetworkVariablesUpdate(short procedureId, int playerID, object content)
        {
            try
            {
                if (content is not IDictionary<string, object> variableValues)
                    return;

                var changed = false;
                foreach (var keyValuePair in variableValues)
                {
                    var v = GetVariables().GetVariable(keyValuePair.Key);
                    if (v != null)
                    {
                        v.RawValue = keyValuePair.Value;
                        _variableChangedMap[v.Name] = v;
                        changed = true;
                    }
                }

                if (changed)
                {
                    try { onVariableChanged?.Invoke(); }
                    catch { /* ignored */ }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private FsmVariables GetVariables()
        {
            return useGlobals ? PlayMakerGlobals.Instance ? PlayMakerGlobals.Instance.Variables : null : fsm ? fsm.FsmVariables : null;
        }
    }
}

#endif