using System;
using JetBrains.Annotations;
using UnityEngine;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    /// <summary>
    /// API for setting the cursor icon.
    /// </summary>
    [HideMonoScript]
    public partial class CursorIconAPI : TriInspectorMonoBehaviour
    {
        [SerializeField] private UnityEvent onCursorTypeChanged;
        [SerializeField] private UnityEvent onCursorTypeChangedToInputField;
        [SerializeField] private UnityEvent onCursorTypeChangedToSelect;
        [SerializeField] private UnityEvent onCursorTypeChangedToGrab;
        [SerializeField] private UnityEvent onCursorTypeChangedToDefault;

        [UsedImplicitly]
        private void Start() { /* for enable/disable toggle */ }

        /// <summary>
        /// Set the cursor to the grab cursor this frame. You can optionally call
        /// <see cref="StopGrabCursor"/> to stop the grab cursor from displaying,
        /// otherwise just stop calling this method to stop the grab cursor.
        /// </summary>
        [UsedImplicitly]
        public void GrabCursor()
        {
            GrabCursorInternal();
        }

        /// <summary>
        /// Stop the grab cursor from displaying.
        /// </summary>
        [UsedImplicitly]
        public void StopGrabCursor()
        {
            StopGrabCursorInternal();
        }

        [UsedImplicitly]
        static partial void GrabCursorInternal();

        [UsedImplicitly]
        static partial void StopGrabCursorInternal();

        [UsedImplicitly]
        public static void ApplyGrabCursor()
        {
            GrabCursorInternal();
        }

        [UsedImplicitly]
        public static void RemoveGrabCursor()
        {
            StopGrabCursorInternal();
        }
    }
}