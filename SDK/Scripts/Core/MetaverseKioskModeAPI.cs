using System;

namespace MetaverseCloudEngine.Unity
{
    [Flags]
    public enum KioskModeOptions
    {
        MetaSpaceLocked = 1,
        OrganizationLocked = 2,
        AccountLocked = 4,
        DialogsDisabled = 8,
    }

    /// <summary>
    /// A class that provides information about the app's kiosk mode. Most information
    /// and functionality occurs on the internal portion of this class.
    /// </summary>
    public static partial class MetaverseKioskModeAPI
    {
        private static bool _isActive;
        
        /// <summary>
        /// Gets the current kiosk mode options.
        /// </summary>
        public static KioskModeOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the current configuration key prefix.
        /// </summary>
        public static string Config { get; set; }

        /// <summary>
        /// Gets a value that indicates when kiosk mode is active.
        /// </summary>
        public static bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                ActivationChanged?.Invoke(_isActive);
            }
        }

        /// <summary>
        /// Invoked when kiosk mode is activated or deactivated.
        /// </summary>
        public static event Action<bool> ActivationChanged;
    }
}