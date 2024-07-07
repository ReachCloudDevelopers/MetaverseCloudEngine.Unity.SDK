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
        public delegate void HitPointsKilledDelegate(int killerPlayerID, DamageGiver giverSource);
        public delegate void DamageGiverKilledHitPointsDelegate(int killerPlayerID, DamageGiver giverSource, HitPoints killed);
        
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
        [Group("On Damage")] [SerializeField] private UnityEvent onDiedWithoutKiller;
        [Group("On Damage")] [SerializeField] private UnityEvent onKilled;
        [Group("On Damage")] [SerializeField] private UnityEvent<GameObject> onKilledByGameObject;

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

        /// <summary>
        /// Invoked when the object's hitpoints value is killed (i.e. reaches 0).
        /// </summary>
        public event HitPointsKilledDelegate Killed;
        
        /// <summary>
        /// Invoked globally when a damage giver kills a hitpoints object.
        /// </summary>
        public static event DamageGiverKilledHitPointsDelegate DamageGiverKilledHitPoints;

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
                networkObject.Initialized += OnNetworkObjectInitialized;
                networkObject.RegisterRPC((short)NetworkRpcType.HitpointsRequestApplyDamage, RPC_ApplyDamage, @override: false);
                networkObject.RegisterRPC((short)NetworkRpcType.HitpointsValue, RPC_SetHitPoints, @override: false);
                networkObject.RegisterRPC((short)NetworkRpcType.RequestHitpointsValue, RPC_RequestHitPoints, @override: false);
            }
        }

        private void OnDestroy()
        {
            if (networked && networkObject)
            {
                networkObject.Initialized -= OnNetworkObjectInitialized;
                networkObject.UnregisterRPC((short)NetworkRpcType.HitpointsRequestApplyDamage, RPC_ApplyDamage);
                networkObject.UnregisterRPC((short)NetworkRpcType.HitpointsValue, RPC_SetHitPoints);
                networkObject.UnregisterRPC((short)NetworkRpcType.RequestHitpointsValue, RPC_RequestHitPoints);
            }
        }

        #endregion

        #region PUBLIC METHODS

        public void Heal(int amount)
        {
            if (IsDead)
                return;
            
            if (amount <= 0)
                return;
            
            var isNetworked = networked && networkObject;
            if (isNetworked)
            {
                if (!networkObject.IsInputAuthority)
                    return;
                
                ChangeHitPointsInternal(-amount);
                
                networkObject.InvokeRPC(
                    (short)NetworkRpcType.HitpointsValue,
                    NetworkMessageReceivers.Others,
                    new object[]
                    {
                        hitPointsIndex,
                        (int)CurrentHitPoints
                    });

                return;
            }
            
            ChangeHitPointsInternal(-amount);
        }

        public void ApplyDamage(DamageGiver giver, object[] args)
        {
            if (IsDead)
                return;

            if (!giver.TryGetDamage(args, out var damage))
                return;

            if (damage <= 0)
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
 
            ChangeHitPointsInternal(giver, damage);

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

        private void OnNetworkObjectInitialized()
        {
            if (networkObject.IsInputAuthority)
                return;
            
            networkObject.InvokeRPC(
                (short)NetworkRpcType.RequestHitpointsValue,
                NetworkMessageReceivers.Others,
                hitPointsIndex);
        }

        private void ChangeHitPointsInternal(int value) => ChangeHitPointsInternal(null, value);

        private void ChangeHitPointsInternal(DamageGiver giver, int value)
        {
            if (value == 0)
                return;

            if (networkObject && !networkObject.IsInputAuthority)
                return;

            var hitPoints = (int)CurrentHitPoints;
            hitPoints -= value;

            if (hitPoints <= 0)
            {
                CurrentHitPoints = 0;
                IsDead = true;

                if (giver)
                {
                    var killerNetworkID = giver.NetworkObject ? giver.NetworkObject.InputAuthorityID : -1;
                    Killed?.Invoke(killerNetworkID, giver);
                    DamageGiverKilledHitPoints?.Invoke(killerNetworkID, giver, this);

                    onKilled?.Invoke();
                    onKilledByGameObject?.Invoke(giver.gameObject);
                    onDied?.Invoke();
                }
                else
                {
                    onDiedWithoutKiller?.Invoke();
                    onDied?.Invoke();
                }

                OnValueChanged();

                return;
            }
            
            if (hitPoints >= hitPointsValue)
                hitPoints = (int)hitPointsValue;

            var delta = hitPoints - (int)CurrentHitPoints;
            if (delta == 0)
                return;
            
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

        private void RPC_RequestHitPoints(short procedureID, int senderID, object content)
        {
            if (!networkObject.IsInputAuthority)
                return;

            if (content is not int index || index != hitPointsIndex)
                return;
            
            networkObject.InvokeRPC(
                (short)NetworkRpcType.HitpointsValue,
                senderID,
                new object[]
                {
                    hitPointsIndex,
                    (int)CurrentHitPoints
                });
        }

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