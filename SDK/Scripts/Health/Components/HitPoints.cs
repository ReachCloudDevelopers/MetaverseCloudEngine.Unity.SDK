using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Health.Components
{
    // NOTE: This will have to be reworked completely for authoritative server models for security.

    /// <summary>
    /// Adds a hitpoints system to the object with this game object.
    /// </summary>
    [DeclareFoldoutGroup("Networking")]
    [DeclareFoldoutGroup("On Value")]
    [DeclareFoldoutGroup("On Damage")]
    [HideMonoScript]
    [Experimental]
    public class HitPoints : TriInspectorMonoBehaviour
    {
        #region INSPECTOR

        [Tooltip("The starting hitpoints value.")] 
        [SerializeField, Min(1)] private uint hitPointsValue = 100;

        [Space]
        [Group("On Value")] [SerializeField] private UnityEvent<int> onValue;
        [Group("On Value")] [SerializeField] private UnityEvent<float> onValueFloat;
        [Tooltip("The string format to use for the 'On Value String' event.")] 
        [Group("On Value")] [SerializeField] private string valueStringFormat = "{0}";
        [Group("On Value")] [SerializeField] private UnityEvent<string> onValueString;

        [Space] 
        [Group("On Damage")] [SerializeField] private UnityEvent<int> onTakeDamage;
        [Group("On Damage")] [SerializeField] private UnityEvent<int> onHealed;
        [Group("On Damage")] [SerializeField] private UnityEvent onDied;

        [Space] 
        [Group("Networking")]
        [SerializeField, Tooltip("Whether to sync the health value over the network.")]
        private bool networked;
        [Group("Networking")]
        [ShowIf(nameof(networkObject))]
        [SerializeField, ReadOnly] private int hitPointsIndex;
        [Group("Networking")]
        [SerializeField, Tooltip("The network object that will be used for synchronization."), Required, ShowIf(nameof(networked))]
        private NetworkObject networkObject;

        #endregion

        #region PROPERTIES

        /// <summary>
        /// The current hitpoints value.
        /// </summary>
        public uint CurrentHitPoints { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the object is "dead" (i.e. out of hitpoints).
        /// </summary>
        public bool IsDead { get; private set; }

        #endregion

        #region PRIVATE METHODS

        private void OnValidate()
        {
            hitPointsIndex = networkObject.GetNetworkObjectBehaviorID(this);
            if (networked && !networkObject)
                networkObject = GetComponentInParent<NetworkObject>();
        }

        private void Awake()
        {
            CurrentHitPoints = hitPointsValue;

            if (networked && networkObject)
            {
                networkObject.RegisterRPC((short)NetworkRpcType.HitpointsRequestApplyDamage, RPC_ApplyDamage,
                    @override: false);
                networkObject.RegisterRPC((short)NetworkRpcType.HitpointsValue, RPC_SetHitPoints, @override: false);
            }
        }

        private void OnDestroy()
        {
            if (networked && networkObject)
            {
                networkObject.UnregisterRPC((short)NetworkRpcType.HitpointsRequestApplyDamage, RPC_ApplyDamage);
                networkObject.UnregisterRPC((short)NetworkRpcType.HitpointsValue, RPC_SetHitPoints);
            }
        }

        #endregion

        #region PUBLIC METHODS

        public void ApplyDamage(DamageGiver giver, object[] args)
        {
            if (IsDead)
                return;

            if (!giver.TryGetDamage(this, args, out var damage))
                return;

            if (damage == 0)
                return;

            var isNetworked = networked && networkObject;
            if (isNetworked && !networkObject.IsInputAuthority)
            {
                if (giver.NetworkID < 0 || giver.DamagerID < 0 || !giver.IsLocalAuthority)
                    return;

                networkObject.InvokeRPC(
                    (short)NetworkRpcType.HitpointsRequestApplyDamage,
                    networkObject.InputAuthorityID,
                    new object[]
                    {
                        hitPointsIndex,
                        giver.NetworkID,
                        giver.DamagerID,
                        args
                    });

                return;
            }

            DamageHitPointsInternal(damage);

            if (!isNetworked) return;
            if (IsDead)
                networkObject.InvokeRPC(
                    (short)NetworkRpcType.HitpointsDied,
                    NetworkMessageReceivers.Others,
                    hitPointsIndex);
            else
                networkObject.InvokeRPC(
                    (short)NetworkRpcType.HitpointsValue,
                    NetworkMessageReceivers.Others,
                    new object[]
                    {
                        hitPointsIndex,
                        (int)CurrentHitPoints
                    });
        }

        #endregion

        #region PRIVATE METHODS

        private void DamageHitPointsInternal(int value)
        {
            if (networkObject && !networkObject.IsStateAuthority)
                return;

            var hitPoints = (int)CurrentHitPoints;
            hitPoints -= value;

            if (hitPoints <= 0)
            {
                CurrentHitPoints = 0;

                IsDead = true;
                onDied?.Invoke();

                OnValueChanged();
                return;
            }

            var delta = hitPoints - (int)CurrentHitPoints;
            CurrentHitPoints = (uint)hitPoints;
            switch (delta)
            {
                case < 0:
                    onTakeDamage?.Invoke(delta);
                    break;
                case > 0:
                    onHealed?.Invoke(delta);
                    break;
            }

            OnValueChanged();
        }

        private void OnValueChanged()
        {
            onValue?.Invoke((int)CurrentHitPoints);
            onValueFloat?.Invoke(CurrentHitPoints);
            onValueString?.Invoke(string.Format(valueStringFormat, CurrentHitPoints));
        }

        #endregion

        #region RPCs

        private void RPC_SetHitPoints(short procedureID, int senderID, object content)
        {
            if (networkObject.InputAuthorityID != senderID)
                return;

            if (content is not object[] { Length: 2 } args)
                return;

            if (args[0] is not int index || index != hitPointsIndex)
                return;

            if (args[1] is not int newHp || newHp < 0)
                return;

            var delta = newHp - (int)CurrentHitPoints;
            CurrentHitPoints = (uint)newHp;
            switch (delta)
            {
                case < 0:
                    onTakeDamage?.Invoke(delta);
                    break;
                case > 0:
                    onHealed?.Invoke(delta);
                    break;
            }
            
            OnValueChanged();
        }

        private void RPC_ApplyDamage(short procedureID, int sendingPlayer, object content)
        {
            if (!networkObject.IsInputAuthority)
                return;

            if (content is not object[] { Length: 4 } args)
                return;

            if (args[0] is not int damageableIndex || damageableIndex < 0 || this.hitPointsIndex != damageableIndex)
                return;

            if (args[1] is not int networkID ||
                args[2] is not int giverID ||
                giverID < 0 ||
                networkID < 0)
                return;

            var giver = DamageGiver.FindNetworkDamageGiver(networkID, giverID);
            if (giver) ApplyDamage(giver, args[3] as object[]);
        }

        #endregion
    }
}