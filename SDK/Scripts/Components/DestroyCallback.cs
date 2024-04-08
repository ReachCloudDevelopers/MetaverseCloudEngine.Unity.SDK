using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This is a helper component that exposes the destroy event of the unity component.
    /// </summary>
    [AddComponentMenu("")]
    public class DestroyCallback : MonoBehaviour
    {
        /// <summary>
        /// Invoked when this component is destroyed.
        /// </summary>
        public event Action<DestroyCallback> Destroyed;

        private void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            Destroyed?.Invoke(this);
        }

        /// <summary>
        /// Cancels the callback and removes the event listeners.
        /// </summary>
        public void Cancel()
        {
            Destroyed = null;
            Destroy(this);
        }
    }

    /// <summary>
    /// Adds an <see cref="OnDestroy(GameObject, Action{DestroyCallback})"/> callback to game objects.
    /// </summary>
    public static class DestroyCallbackExtensions
    {
        /// <summary>
        /// Listen for the Destroy event of a game object.
        /// </summary>
        /// <param name="obj">The object to listen to.</param>
        /// <param name="onDestroyed">The event that's invoked when the object is destroyed.</param>
        /// <returns></returns>
        public static DestroyCallback OnDestroy(this GameObject obj, Action<DestroyCallback> onDestroyed)
        {
            if (onDestroyed == null) return null;
            if (!obj) return null;
            var notifier = obj.AddComponent<DestroyCallback>();
            notifier.Destroyed += onDestroyed;
            return notifier;
        }
    }
}
