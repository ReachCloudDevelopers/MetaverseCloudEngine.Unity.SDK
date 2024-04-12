using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV
{
    [RequireComponent(typeof(ITextureToMatrixProvider))]
    public class DepthEstimationWithQrCodeDetection : TriInspectorMonoBehaviour, IObjectDetectionPipeline
    {
        private QRCodeDetector _detector;
        private ITextureToMatrixProvider _textureProvider;
        private readonly Mat _points = new();
        private readonly List<string> _detectionsNonAlloc = new();

        public event Action<List<IObjectDetectionPipeline.DetectedObject>> DetectableObjectsUpdated;

        private void Awake()
        {
            _detector = new QRCodeDetector();
            _textureProvider = GetComponent<ITextureToMatrixProvider>();
        }

        private void Update()
        {
            var detectedObjects = new List<IObjectDetectionPipeline.DetectedObject>();
            using var frame = _textureProvider.DequeueNextFrame();
            using var frameMat = frame?.GetMat();
            if (frame is not null && frameMat is not null)
            {
                using var inferenceMat = new Mat();
                Imgproc.cvtColor(frameMat, inferenceMat, Imgproc.COLOR_RGBA2BGR);

                if (_detector.detectAndDecodeMulti(inferenceMat, _detectionsNonAlloc, _points) &&
                    _detectionsNonAlloc.Count != 0)
                    for (var objectIndex = 0; objectIndex < _points.size(0); objectIndex++)
                    {
                        if (_detectionsNonAlloc.Count <= objectIndex)
                            continue;

                        var label = _detectionsNonAlloc[objectIndex];
                        if (string.IsNullOrWhiteSpace(label))
                            continue;

                        var p0 = _points.get(new[] { objectIndex, 0 }); // bottom left
                        var p1 = _points.get(new[] { objectIndex, 1 }); // bottom right
                        var p2 = _points.get(new[] { objectIndex, 2 }); // top right
                        var p3 = _points.get(new[] { objectIndex, 3 }); // top left

                        if (p0 is null || p1 is null || p2 is null || p3 is null)
                            continue;

                        var v0 = new Vector2((float)p0[0], (float)p0[1]);
                        var v1 = new Vector2((float)p1[0], (float)p1[1]);
                        var v2 = new Vector2((float)p2[0], (float)p2[1]);
                        var v3 = new Vector2((float)p3[0], (float)p3[1]);

                        var twoDCoords = (v0 + v1 + v2 + v3) / 4f;
                        if (!frame.TryGetCameraRelativePoint((int)twoDCoords.x, (int)twoDCoords.y, out var vertex))
                            continue;

                        detectedObjects.Add(new IObjectDetectionPipeline.DetectedObject
                        {
                            Label = label,
                            Vertices = new List<Vector3>
                            {
                                vertex
                            },
                            Score = 1,
                            IsBackground = false,
                            NearestZ = vertex.z,
                        });
                    }
            }

            DetectableObjectsUpdated?.Invoke(detectedObjects);
        }
    }
}