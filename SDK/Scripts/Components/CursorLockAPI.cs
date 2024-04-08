using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A component that allows you to lock and unlock the cursor.
    /// </summary>
    [HideMonoScript]
    [DeclareFoldoutGroup("Options")]
    [DeclareFoldoutGroup("Events")]
    public class CursorLockAPI : TriInspectorMonoBehaviour
    {
        [Tooltip("If checked, the cursor will be locked when this component is enabled.")]
        [Group("Options")][SerializeField] private bool lockOnEnable;
        [Tooltip("If checked, the cursor will be unlocked when this component is disabled.")]
        [Group("Options")][SerializeField] private bool unlockOnDisable;
        [Tooltip("If checked, the cursor will be considered locked on mobile platforms.")]
        [Group("Options")][SerializeField] private bool mobileIsLocked = true;
        [Tooltip("If checked, the cursor will not be locked or unlocked if this component is disabled.")]
        [Group("Options")][SerializeField] private bool ignoreIfInactive = true;

        [FormerlySerializedAs("onMouseLockChanged")]
        [Group("Events")] public UnityEvent<bool> onCursorLockChanged = new();
        [FormerlySerializedAs("onMouseLocked")]
        [Group("Events")] public UnityEvent onCursorLocked = new();
        [FormerlySerializedAs("onMouseUnlocked")]
        [Group("Events")] public UnityEvent onCursorUnlocked = new();

        private bool _lockValue;
        private bool _started;

        /// <summary>
        /// If true, the cursor will be locked when this component is enabled.
        /// </summary>
        public bool LockOnEnable {
            get => lockOnEnable;
            set => lockOnEnable = value;
        }

        /// <summary>
        /// If true, the cursor will be unlocked when this component is disabled.
        /// </summary>
        public bool UnlockOnDisable {
            get => unlockOnDisable;
            set => unlockOnDisable = value;
        }

        /// <summary>
        /// Gets a value indicating whether the cursor is locked.
        /// </summary>
        private bool IsCursorLocked {
            get {
                if (UnityEngine.Device.Application.isMobilePlatform)
                    return mobileIsLocked;
                return Cursor.lockState == CursorLockMode.Locked;
            }
        }

        private void Awake()
        {
            if (UnityEngine.Device.Application.isMobilePlatform)
                return;

            onCursorLockChanged.AddListener(l =>
            {
                if (l) onCursorLocked?.Invoke();
                else onCursorUnlocked?.Invoke();
            });
        }

        private void Start()
        {
            onCursorLockChanged.Invoke(IsCursorLocked);

            if (!lockOnEnable && UnityEngine.Device.Application.platform != RuntimePlatform.WebGLPlayer)
                UpdateMouseLock(IsCursorLocked);
            
            _started = true;
        }

        private void OnEnable()
        {
            if (lockOnEnable && UnityEngine.Device.Application.platform != RuntimePlatform.WebGLPlayer)
                SetIsLocked(true);
            else if (_started)
                onCursorLockChanged?.Invoke(IsCursorLocked);
        }

        private void OnDisable()
        {
            if (unlockOnDisable)
                SetIsLocked(false);
        }

#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR 
        private void Update()
        {
            var isLocked = IsCursorLocked;
            if (isLocked == _lockValue) return;
            _lockValue = isLocked;
            onCursorLockChanged?.Invoke(isLocked);
        }
#endif

        /// <summary>
        /// Locks or unlocks the cursor.
        /// </summary>
        /// <param name="value">If true, the cursor will be locked.</param>
        public void SetIsLocked(bool value)
        {
            if (ignoreIfInactive && !enabled) return;
            if (UnityEngine.Device.Application.isMobilePlatform) return;
            UpdateMouseLock(value);
            _lockValue = IsCursorLocked;
        }

        private void UpdateMouseLock(bool value)
        {
            if (!enabled) return;
            if (UnityEngine.Device.Application.isMobilePlatform) return;
            if (value == IsCursorLocked) return;
            if (value)
            {
                if (!MetaverseCursorAPI.TryLockCursor())
                    return;
            }
            else MetaverseCursorAPI.UnlockCursor();
            onCursorLockChanged?.Invoke(IsCursorLocked);
        }
    }
}
