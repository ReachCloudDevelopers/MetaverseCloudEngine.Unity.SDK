using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    /// <summary>
    /// An interface for a pipeline that can detect objects in the camera feed.
    /// </summary>
    public interface IObjectDetectionPipeline
    {
        /// <summary>
        /// Invoked when the pipeline has detected objects in the camera feed.
        /// </summary>
        event Action<List<DetectedObject>> DetectableObjectsUpdated;

        public sealed class DetectedObject
        {
            /// <summary>
            /// The label of the object.
            /// </summary>
            public string Label;
            /// <summary>
            /// True if this is the environment.
            /// </summary>
            public bool IsBackground;
            /// <summary>
            /// The object's on-screen rect.
            /// </summary>
            public Vector4 Rect;
            /// <summary>
            /// The object's vertices in camera-relative space.
            /// </summary>
            public List<Vector3> Vertices;
            /// <summary>
            /// The object's origin relative to the camera.
            /// </summary>
            public Vector3 Origin;
            /// <summary>
            /// The nearest Z value of all vertices.
            /// </summary>
            public float NearestZ;
            /// <summary>
            /// The confidence score of this object.
            /// </summary>
            public float Score;
        }
    }
}