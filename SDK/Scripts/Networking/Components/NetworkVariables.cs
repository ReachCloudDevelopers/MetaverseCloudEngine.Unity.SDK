using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using MetaverseCloudEngine.Unity.Scripting.Components;
using TriInspectorMVCE;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    /// <summary>
    /// Syncs the values of the variables in the <see cref="Variables"/> component. Only the variables that are
    /// changed will be synced.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Networking/Network Variables")]
    public class NetworkVariables : NetworkObjectBehaviour
    {
        #region Inspector
        
        [Tooltip("If true, the variables will be taken from the active scene variables.")]
        public bool useSceneVariables;
        [Tooltip("The variables to sync if not using the scene variables.")]
        [DisallowNull, HideIf(nameof(useSceneVariables)), HideIf(nameof(UseScript))] 
        public Variables variables;
        [Tooltip("The script whose variables to sync.")]
        [DisallowNull, HideIf(nameof(useSceneVariables)), HideIf(nameof(UseVariables))]
        public MetaverseScript script;
        [Tooltip("The names of the specific variables to sync. If empty, no variables will be synced.")]
        public string[] variablesToSync = Array.Empty<string>();
        [Tooltip("Invoked when a variable is changed.")]
        [Space]
        public UnityEvent onVariableChanged;

        #endregion

        #region Private Fields

        private bool _isInitialSend = true;
        private readonly Dictionary<string, object> _variableChangedMap = new();
        private VariableDeclarations _declarations;

        #endregion

        #region Properties

        public bool UseVariables => variables;
        public bool UseScript => script;
        
        /// <summary>
        /// The variable declarations to sync.
        /// </summary>
        public VariableDeclarations Declarations => _declarations ??= useSceneVariables && Variables.ExistInActiveScene ? Variables.ActiveScene : script ? script.Vars : variables ? variables.declarations : null;

        #endregion

        #region Unity Events
        
        private void Reset()
        {
            if (!useSceneVariables && !variables) variables = GetComponent<Variables>();
            if (!variables) script = GetComponent<MetaverseScript>();
        }

        protected override void Awake()
        {
            if (!useSceneVariables && !variables) variables = GetComponent<Variables>();
            if (!variables) script = GetComponent<MetaverseScript>();
            base.Awake();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (useSceneVariables)
            {
                variables = null;
                script = null;
            }
            if (variables) script = null;
            if (script) variables = null;
        }

        private void FixedUpdate()
        {
            NetworkUpdate();
        }

        #endregion

        #region Protected Methods
        
        protected override void RegisterNetworkRPCs()
        {
            NetworkObject.RegisterRPC((short)NetworkRpcType.NetworkVariablesUpdate, RPC_NetworkVariablesUpdate, @override: false);
            NetworkObject.RegisterRPC((short)NetworkRpcType.NetworkVariablesRequest, RPC_NetworkVariablesRequest, @override: false);
        }

        protected override void UnRegisterNetworkRPCs()
        {
            NetworkObject.UnregisterRPC((short)NetworkRpcType.NetworkVariablesUpdate, RPC_NetworkVariablesUpdate);
            NetworkObject.UnregisterRPC((short)NetworkRpcType.NetworkVariablesRequest, RPC_NetworkVariablesRequest);
        }

        #endregion

        #region Private Methods
        
        private void NetworkUpdate()
        {
            if (!NetworkObject)
            {
                enabled = false;
                return;
            }

            if (Declarations == null)
            {
                enabled = false;
                return;
            }

            if (NetworkObject.IsInitialized && NetworkObject.IsStateAuthority)
                SendDirtyVariables();
        }

        private void SendDirtyVariables(bool sendAll = false, int? targetPlayer = null)
        {
            bool isDirty = false;
            Dictionary<string, object> variablesToSend = null;
            if (Declarations == null)
                return;
            
            for (int i = variablesToSync.Length - 1; i >= 0; i--)
            {
                string variableName = variablesToSync[i];
                if (!Declarations.IsDefined(variableName))
                    continue;

                object variableValue = Declarations.Get(variableName);
                if (!sendAll && _variableChangedMap.TryGetValue(variableName, out object cachedValue) && cachedValue == variableValue)
                    continue;

                _variableChangedMap[variableName] = variableValue;
                variablesToSend ??= new Dictionary<string, object>();
                if (variableValue is not null && variableValue is not string)
                {
                    Type variableType = variableValue.GetType();
                    if (!variableType.IsPrimitive)
                        continue; // skip all non primitive types
                }

                variablesToSend[variableName] = variableValue;
                isDirty = true;
            }

            if (!isDirty) return;
            if (targetPlayer != null) NetworkObject.InvokeRPC((short)NetworkRpcType.NetworkVariablesUpdate, targetPlayer.Value, variablesToSend);
            else NetworkObject.InvokeRPC((short)NetworkRpcType.NetworkVariablesUpdate, NetworkMessageReceivers.Others, variablesToSend);

            try { onVariableChanged?.Invoke(); }
            catch { /* ignored */ }
        }

        #endregion

        #region Public Methods

        public override void OnLocalStateAuthority()
        {
            if (!_isInitialSend) return;
            SendDirtyVariables(sendAll: _isInitialSend);
            _isInitialSend = false;
        }

        public override void OnRemoteStateAuthority()
        {
            try
            {
                _isInitialSend = true;
                NetworkObject.InvokeRPC((short)NetworkRpcType.NetworkVariablesRequest, NetworkObject.StateAuthorityID, NetworkObject.Networking.LocalPlayerID);
            }
            catch (Exception e)
            {
                /* ignored */
            }
        }

        #endregion

        #region RPCs

        private void RPC_NetworkVariablesRequest(short procedureId, int sendingPlayer, object content)
        {
            SendDirtyVariables(true, (int)content);
        }

        private void RPC_NetworkVariablesUpdate(short procedureId, int sendingPlayer, object content)
        {
            if (content is not IDictionary<string, object> variableValues)
                return;

            if (Declarations == null)
                return;

            bool changed = false;
            foreach (KeyValuePair<string, object> keyValuePair in variableValues)
            {
                if (!Declarations.IsDefined(keyValuePair.Key)) continue;
                Declarations.Set(keyValuePair.Key, keyValuePair.Value);
                changed = true;
            }

            if (changed)
            {
                try { onVariableChanged?.Invoke(); }
                catch { /* ignored */ }
            }
        }

        #endregion
    }
}