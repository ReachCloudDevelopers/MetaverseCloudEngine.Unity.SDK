﻿using MetaverseCloudEngine.Unity.Networking.Components;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Health.Components
{
    [HideMonoScript]
    [Experimental]
    public abstract class DamageGiver : TriInspectorMonoBehaviour
    {
        [InfoBox("(Optional) Specify network object to sync damage on the network.")]
        [SerializeField] private NetworkObject networkObject;
        [SerializeField, ReadOnly] private int networkDamagerID;

        private static readonly Dictionary<(int n, int d), DamageGiver> DamageGiverLookup = new();

        private NetworkObject NetworkObject {
            get {
                if (!networkObject)
                    networkObject = GetComponentInParent<NetworkObject>();
                return networkObject;
            }
        }

        public int DamagerID => networkDamagerID;
        public int NetworkID => NetworkObject != null ? NetworkObject.NetworkID : -1;
        public bool IsLocalAuthority => NetworkObject != null && NetworkObject.IsStateAuthority;

        protected virtual void Awake()
        {
            if (!NetworkObject) return;
            if (NetworkObject.IsInitialized)
                OnNetworkObjectInit();
            else
                NetworkObject.Initialized += OnNetworkObjectInit;
        }

        protected virtual void OnDestroy()
        {
            if (NetworkID != -1 && DamagerID != -1)
                DamageGiverLookup.Remove((NetworkID, DamagerID));
        }

        private void OnValidate()
        {
            networkDamagerID = NetworkObject.GetNetworkObjectBehaviorID(this);
        }

        public abstract bool TryGetDamage(HitPoints hp, object[] arguments, out int damage);

        private void OnNetworkObjectInit()
        {
            if (DamagerID != -1)
                DamageGiverLookup[(NetworkObject.NetworkID, DamagerID)] = this;
        }

        public static DamageGiver FindNetworkDamageGiver(int networkID, int giverID)
        {
            return DamageGiverLookup.TryGetValue((networkID, giverID), out var value) ? value : null;
        }
    }
}