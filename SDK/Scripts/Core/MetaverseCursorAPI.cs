using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    public static class MetaverseCursorAPI
    {
        private static float _nextCursorLockTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Application.focusChanged += OnFocusChanged;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (!focused)
                UnlockCursor();
        }

        public static void UnlockCursor(bool webGLTimeout = true)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer && webGLTimeout && Cursor.lockState == CursorLockMode.Locked)
                _nextCursorLockTime = Time.time + 1.25f;
            if (UnityEngine.Device.Application.isMobilePlatform) return;
            Cursor.lockState = CursorLockMode.None;
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                Cursor.visible = true;
        }

        public static bool TryLockCursor()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (_nextCursorLockTime > Time.time)
                {
                    // FIXME: WEBGL Chrome / Edge / etc. does not allow cursor lock
                    // unless you wait at least 1.25 seconds to lock the cursor again.
                    // Need to figure out how to properly display that to the user.
                    MetaverseProgram.Logger.Log("Cannot lock cursor for " + (_nextCursorLockTime - Time.time) + " second(s).");
                    return false;
                }
            }

            if (!UnityEngine.Device.Application.isMobilePlatform)
                Cursor.lockState = CursorLockMode.Locked;
            return true;
        }
    }
}
