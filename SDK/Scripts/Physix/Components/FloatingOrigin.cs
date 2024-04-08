using System;
using UnityEngine;
using UnityEngine.Events;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// The floating origin source object. Should be the main camera usually.
    /// </summary>
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Floating Origin")]
    [HideMonoScript]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(int.MaxValue)]
    public class FloatingOrigin : TriInspectorMonoBehaviour
    {
        [Tooltip("The snapping distance of the floating origin.")]
        public float snapDistance = 1000f;
        [Tooltip("An event that's invoked when the origin has snapped.")]
        public UnityEvent onShifted = new();

        private Transform _transform;

        /// <summary>
        /// Invoked when the origin point is shifted.
        /// </summary>
        public static event Action Shifted;

        /// <summary>
        /// The current floating origin offset.
        /// </summary>
        public static Vector3 OriginPoint { get; private set; }

        /// <summary>
        /// The current floating origin.
        /// </summary>
        public static FloatingOrigin Instance { get; private set; }

        /// <summary>
        /// Whether or not <see cref="Instance"/> exists.
        /// </summary>
        public static bool Exists { get; private set; }

        private void Start()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            _transform = transform;

            Instance = this;
            Exists = true;
            OriginPoint = Vector3.zero;
            Shifted?.Invoke();
            
            Application.onBeforeRender += ApplicationOnBeforeRender;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Exists = false;
                OriginPoint = Vector3.zero;
                Instance = null;
            }
            
            Application.onBeforeRender -= ApplicationOnBeforeRender;
        }

        private void Update()
        {
            Snap();
        }

        private void LateUpdate()
        {
            Snap();
        }

        private void FixedUpdate()
        {
            Snap();
        }

        private void Snap()
        {
            if (_transform.position.magnitude < snapDistance) return;
            
            try
            {
                var delta = _transform.position;
                OriginPoint -= delta;

                for (var i = FloatingOriginObject.All.Count - 1; i >= 0; i--)
                {
                    var obj = FloatingOriginObject.All[i];
                    if (!obj || obj.transform.parent) continue;
                    obj.transform.Translate(-delta, Space.World);
                    if (obj.TryGetComponent(out Rigidbody rb)) 
                        rb.position = obj.transform.position;
                }
                
                _transform.position = Vector3.zero;

                Physics.SyncTransforms();

                for (var i = FloatingOriginObject.All.Count - 1; i >= 0; i--)
                {
                    var obj = FloatingOriginObject.All[i];
                    if (!obj || obj.transform.parent) continue;
                    obj.OnShifted();
                }
                    
                onShifted?.Invoke();
                Shifted?.Invoke();
            }
            finally
            {
            }
        }

        private void ApplicationOnBeforeRender()
        {
            Snap();
        }

        /// <summary>
        /// Converts the Unity-space coordinates of a position into Floating-Origin-space coordinates.
        /// </summary>
        /// <param name="unitySpace">The unity space coordinates.</param>
        /// <returns></returns>
        public static Vector3 UnityToOrigin(Vector3 unitySpace)
        {
            return unitySpace - OriginPoint;
        }

        /// <summary>
        /// Converts the Floating-Origin-space coordinates into Unity-space coordinates.
        /// </summary>
        /// <param name="originSpace">The floating origin-space coordinates.</param>
        /// <returns></returns>
        public static Vector3 OriginToUnity(Vector3 originSpace)
        {
            return originSpace + OriginPoint;
        }
    }
}
