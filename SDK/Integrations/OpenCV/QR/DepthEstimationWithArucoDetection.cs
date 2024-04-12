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
    public class DepthEstimationWithArucoDetection : TriInspectorMonoBehaviour, IObjectDetectionPipeline
    {
        private ArucoDetector _detector;
        private ITextureToMatrixProvider _textureProvider;
        private readonly Mat _ids = new();
        private readonly List<Mat> _corners = new();

        public event Action<List<IObjectDetectionPipeline.DetectedObject>> DetectableObjectsUpdated;

        private void Awake()
        {
            _detector = new ArucoDetector();
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
                _detector.detectMarkers(inferenceMat, _corners, _ids);
                
                if (!_ids.empty() &&
                    _corners.Count > 0)
                    for (var objectIndex = 0; objectIndex < _ids.size(0); objectIndex++)
                    {
                        if (_corners.Count <= objectIndex)
                            continue;

                        var corners = _corners[objectIndex];
                        var label = _ids.get(new [] { objectIndex, 0 })[0];
                        var p0 = corners.get(new[] { 0, 0 }); // bottom left
                        var p1 = corners.get(new[] { 0, 1 }); // bottom right
                        var p2 = corners.get(new[] { 0, 2 }); // top right
                        var p3 = corners.get(new[] { 0, 3 }); // top left

                        var v0 = new Vector2((float)p0[0], (float)p0[1]);
                        var v1 = new Vector2((float)p1[0], (float)p1[1]);
                        var v2 = new Vector2((float)p2[0], (float)p2[1]);
                        var v3 = new Vector2((float)p3[0], (float)p3[1]);

                        var twoDCoords = (v0 + v1 + v2 + v3) / 4f;
                        if (!frame.TryGetCameraRelativePoint((int)twoDCoords.x, (int)twoDCoords.y, out var vertex))
                            continue;

                        detectedObjects.Add(new IObjectDetectionPipeline.DetectedObject
                        {
                            Label = ((int)label).ToString(),
                            Vertices = new List<Vector3>
                            {
                                vertex
                            },
                            Score = 1,
                            Origin = vertex,
                            Rect = new Vector4(v0.x, v0.y, v2.x, v2.y),
                            IsBackground = false,
                            NearestZ = vertex.z,
                        });
                    }

                DetectableObjectsUpdated?.Invoke(detectedObjects);
            }
        }
    }
}