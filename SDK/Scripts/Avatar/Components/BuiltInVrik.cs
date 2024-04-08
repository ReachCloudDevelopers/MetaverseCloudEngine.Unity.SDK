#pragma warning disable CS0414

using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    public partial class BuiltInVrik : MonoBehaviour
    {
        [SerializeField] private Transform rootTransform;
        [SerializeField] private bool calibrateOnSetup = true;
        [SerializeField] private PlayerAvatarContainer avatarContainer;
        [FormerlySerializedAs("trackers")] 
        [SerializeField] private VrTrackers vrTrackers;

        public UnityEvent onCalibrate;
        
        private Animator _avatar;
        private bool _isSetup;
        private GameObject _lHandPlaceholder;
        private GameObject _rHandPlaceholder;

        private void Awake()
        {
            if (!avatarContainer) return;
            if (avatarContainer.Avatar) OnAvatarSpawned(avatarContainer.Avatar);
            avatarContainer.Events.onAvatarSpawned.AddListener(OnAvatarSpawned);
            avatarContainer.Events.onAvatarDeSpawned.AddListener(OnAvatarDeSpawned);
        }

        private void OnDestroy()
        {
            if (avatarContainer)
            {
                avatarContainer.Events.onAvatarSpawned.RemoveListener(OnAvatarSpawned);
                avatarContainer.Events.onAvatarDeSpawned.RemoveListener(OnAvatarDeSpawned);
            }
        }

        private void OnEnable()
        {
            TogglePaceHolderMode(true); 
            OnEnableInternal();
        }

        private void OnDisable()
        {
            TogglePaceHolderMode(false);
            OnDisableInternal();
        }

        private void OnAvatarDeSpawned()
        {
            OnAvatarDeSpawnedInternal();
        }
        
        partial void OnAvatarDeSpawnedInternal();

        private void Reset()
        {
            EnsureAvatarContainer();
            var rootAnimator = GetComponentInParent<Animator>();
            if (rootAnimator)
                rootTransform = rootAnimator.transform;
        }

        private void EnsureAvatarContainer()
        {
            if (!avatarContainer) avatarContainer = gameObject.GetNearestComponent<PlayerAvatarContainer>();
            if (!avatarContainer) avatarContainer = gameObject.AddComponent<PlayerAvatarContainer>();
        }

        private void OnAvatarSpawned(Animator avatar)
        {
            MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                if (!this)
                    return;

                _avatar = avatar;
                _isSetup = false;
                SetupIKInternal(ref _isSetup);

                if (!_isSetup)
                {
                    DestroyPlaceholders();
                    SpawnPlaceholders();
                    TogglePaceHolderMode(enabled);
                }
            });
        }

        private void SpawnPlaceholders()
        {   
            CreatePlaceholderHand(ref _lHandPlaceholder, vrTrackers.LHand, "XRLController");
            CreatePlaceholderHand(ref _rHandPlaceholder, vrTrackers.RHand, "XRRController");
        }

        private static void CreatePlaceholderHand(ref GameObject reference, Transform target, string resource)
        {
            if (reference || !target)
                return;

            var xrControllerPrefab = Resources.Load<GameObject>(MetaverseConstants.Resources.ResourcesBasePath + resource);
            if (xrControllerPrefab) reference = Instantiate(xrControllerPrefab, target);
            else
            {
                reference = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reference.transform.SetParent(target);
                reference.transform.ResetLocalTransform();
                reference.transform.localScale = Vector3.one * 0.1f;
                if (reference.TryGetComponent(out Collider col))
                    Destroy(col);
            }
        }

        private void DestroyPlaceholders()
        {
            if (_lHandPlaceholder) Destroy(_lHandPlaceholder);
            if (_rHandPlaceholder) Destroy(_rHandPlaceholder);
        }

        private void TogglePaceHolderMode(bool value)
        {
            if (_isSetup) return;
            if (_avatar) _avatar.gameObject.SetActive(!value);
            if (_lHandPlaceholder) _lHandPlaceholder.SetActive(value);
            if (_rHandPlaceholder) _rHandPlaceholder.SetActive(value);
        }

        partial void SetupIKInternal(ref bool isSetup);

        partial void OnEnableInternal();

        partial void OnDisableInternal();
    }
}
