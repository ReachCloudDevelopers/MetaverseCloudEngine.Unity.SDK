using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering
{
    /// <summary>
    /// A helper component that allows objects to measure their distance from the <see cref="Camera.main"/>.
    /// </summary>
    [AddComponentMenu("")]
    public class CameraDistanceManager : MonoBehaviour
    {
        private readonly List<IMeasureCameraDistance> _cameraDistanceMeasurers = new();
        private readonly List<IMeasureCameraDistance> _measurersToRemove = new();
        private readonly CancellationTokenSource _cancellationToken = new();
        private static CameraDistanceManager _instance;

        /// <summary>
        /// The current instance of the <see cref="CameraDistanceManager"/>. Note: This will create
        /// a new instance in the scene if one is not already there. This only works in play mode.
        /// </summary>
        public static CameraDistanceManager Instance {
            get {
                if (!Application.isPlaying)
                    return null;
                if (_instance) return _instance;
                _instance = FindObjectOfType<CameraDistanceManager>();
                if (_instance) return _instance;
                _instance = new GameObject(nameof(CameraDistanceManager)).AddComponent<CameraDistanceManager>();
                _instance.gameObject.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                return _instance;
            }
        }

        private void Awake()
        {
            UniTask.Void(CheckCameraDistance, _cancellationToken.Token);
        }

        private void OnDestroy()
        {
            _cancellationToken.Cancel();
            _cameraDistanceMeasurers.Clear();
        }

        private async UniTaskVoid CheckCameraDistance(CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(PlayerLoopTiming.FixedUpdate);

            var mainCam = Camera.main;
            while (!_cancellationToken.IsCancellationRequested)
            {
                for (var i = _measurersToRemove.Count - 1; i >= 0; i--)
                {
                    _cameraDistanceMeasurers.Remove(_measurersToRemove[i]);
                    _measurersToRemove.RemoveAt(i);
                }

                if (!mainCam)
                    mainCam = Camera.main;

                if (mainCam)
                {
                    var mainCamPos = mainCam.transform.position;
                    const int maxPerFrame = 25;
                    for (var i = _cameraDistanceMeasurers.Count - 1; i >= 0; i--)
                    {
                        var measurer = _cameraDistanceMeasurers[i];
                        if (measurer == null || !(measurer as Object))
                        {
                            _cameraDistanceMeasurers.RemoveAt(i);
                            continue;
                        }

                        measurer.OnCameraDistance(mainCam, Vector3.SqrMagnitude(mainCamPos - measurer.CameraMeasurementPosition));

                        if (i % maxPerFrame == 0)
                        {
                            await UniTask.Yield();
                            if (mainCam)
                                mainCamPos = mainCam.transform.position;
                        }

                        if (_cancellationToken.IsCancellationRequested)
                            return;
                    }
                }

                await UniTask.Yield();
            }
        }

        public void AddMeasurer(IMeasureCameraDistance measurer)
        {
            if (!_cameraDistanceMeasurers.Contains(measurer))
                _cameraDistanceMeasurers.Add(measurer);
            _measurersToRemove.Remove(measurer);
        }

        public void RemoveMeasurer(IMeasureCameraDistance measurer)
        {
            _measurersToRemove.Add(measurer);
        }
    }
}