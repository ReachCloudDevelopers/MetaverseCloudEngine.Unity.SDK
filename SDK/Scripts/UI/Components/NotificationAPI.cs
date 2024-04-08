using MetaverseCloudEngine.Unity.UI.Enumerations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    /// <summary>
    /// An API for displaying notifications and dialogs.
    /// </summary>
    [HideMonoScript]
    public partial class NotificationAPI : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The title of the notification.
        /// </summary>
        [field: Header("Notification Options")]
        [field: SerializeField] public string Title { get; set; }
        /// <summary>
        /// The message of the notification.
        /// </summary>
        [field: SerializeField] public string Message { get; set; }
        /// <summary>
        /// The type of the notification.
        /// </summary>
        [field: SerializeField] public NotificationType Type { get; set; } = NotificationType.None;

        /// <summary>
        /// The text to display on the ok button.
        /// </summary>
        [field: Header("Dialog Options")]
        [field: SerializeField] public string OkButtonText { get; set; } = "Ok";
        /// <summary>
        /// The text to display on the cancel button.
        /// </summary>
        [field: SerializeField] public string CancelButtonText { get; set; }
        /// <summary>
        /// The text to display on the input field (required to "ok" the dialog).
        /// </summary>
        [field: SerializeField] public string InputText { get; set; }
        /// <summary>
        /// The text to display on the input field placeholder.
        /// </summary>
        [field: SerializeField] public bool Force { get; set; }

        [Tooltip("The event to invoke when the ok button is clicked.")]
        public UnityEvent onOk;
        [Tooltip("The event to invoke when the cancel button is clicked.")]
        public UnityEvent onCancel;
        
        public void ShowNotification() => ShowNotification(string.Empty);

        public void ShowNotification(string message) => ShowNotificationInternal(message);

        partial void ShowNotificationInternal(string message);

        public void ShowDialog() => ShowDialog(string.Empty);

        public void ShowDialog(string message)
        {
            ShowDialogInternal(message);
            
#if !METAVERSE_CLOUD_ENGINE_INTERNAL
            onOk?.Invoke();
#endif
        }
        
        partial void ShowDialogInternal(string message);
    }
}