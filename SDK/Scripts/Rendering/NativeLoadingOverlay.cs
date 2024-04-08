using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering
{
    public class NativeLoadingOverlay : IDisposable
    {
        private Camera _blackoutCamera;
        
        public static NativeLoadingOverlay Create()
        {
            GameObject blackoutCameraObject = null;
            try
            {
                var screen = new NativeLoadingOverlay();
            
                // Create a new camera
                blackoutCameraObject = new GameObject(nameof(NativeLoadingOverlay) + ".Camera");
                var blackoutCamera = blackoutCameraObject.AddComponent<Camera>();
                // Set the depth to a high value so it renders last
                blackoutCamera.depth = 100;
                // Set clear flags to Solid Color and background color to black
                blackoutCamera.clearFlags = CameraClearFlags.SolidColor;
                // Slightly lighter than black to avoid people thinking the screen is off
                blackoutCamera.backgroundColor = new Color(0.01f, 0.01f, 0.01f, 1f);
                // Set culling mask to Nothing to avoid rendering any unnecessary layers
                blackoutCamera.cullingMask = 0;
            
                screen._blackoutCamera = blackoutCamera;
                return screen;
            }
            catch
            {
                if (blackoutCameraObject)
                    UnityEngine.Object.Destroy(blackoutCameraObject);
                throw;
            }
        }
        
        public void Dispose()
        {
            if (!_blackoutCamera) return;
            UnityEngine.Object.Destroy(_blackoutCamera.gameObject, 1f);
            _blackoutCamera = null;
        }
    }
}