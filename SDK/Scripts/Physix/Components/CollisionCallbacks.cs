using System;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Physix
{
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Collision Callbacks")]
    public class CollisionCallbacks : TriInspectorMonoBehaviour
    {
        [Serializable]
        [DeclareFoldoutGroup("Requirements")]
        [DeclareFoldoutGroup("On Collision Enter")]
        [DeclareFoldoutGroup("On Collision Exit")]
        public class CollisionHandler
        {
            [Tooltip("(Editor Only) The name of this collision handler. For easier reading.")]
            [HideLabel] public string title;

            public bool checkAttachedRigidbody = true;

            [Group("Requirements")][Min(0)] public float minForceThreshold;
            [Group("Requirements")][Min(0)] public float minObjectMass;
            [Group("Requirements")] public LayerMask requiredLayers = ~Physics.AllLayers;
            [Group("Requirements")] public List<string> requiredTags = new();
            [Group("Requirements")] public List<string> requiredNames = new();

            [Group("On Collision Enter")] public UnityEvent<GameObject> onCollisionEnter = new();
            [Group("On Collision Enter")] public UnityEvent<Transform> onCollisionEnterTransform = new();
            [Group("On Collision Enter")] public UnityEvent<Collider> onCollisionEnterCollider = new();
            [Group("On Collision Enter")] public UnityEvent<Collider2D> onCollisionEnterCollider2D = new();
            [Group("On Collision Enter")] public UnityEvent<Rigidbody> onCollisionEnterRigidbody = new();
            [Group("On Collision Enter")] public UnityEvent<Rigidbody2D> onCollisionEnterRigidbody2D = new();
            [Group("On Collision Enter")] public UnityEvent<Collision> onCollisionEnterInfo = new();
            [Group("On Collision Enter")] public UnityEvent<Collision2D> onCollisionEnterInfo2D = new();

            [Group("On Collision Exit")] public UnityEvent<GameObject> onCollisionExit = new();
            [Group("On Collision Exit")] public UnityEvent<Transform> onCollisionExitTransform = new();
            [Group("On Collision Exit")] public UnityEvent<Collider> onCollisionExitCollider = new();
            [Group("On Collision Exit")] public UnityEvent<Collider2D> onCollisionExitCollider2D = new();
            [Group("On Collision Exit")] public UnityEvent<Rigidbody> onCollisionExitRigidbody = new();
            [Group("On Collision Exit")] public UnityEvent<Rigidbody2D> onCollisionExitRigidbody2D = new();
            [Group("On Collision Exit")] public UnityEvent<Collision> onCollisionExitInfo = new();
            [Group("On Collision Exit")] public UnityEvent<Collision2D> onCollisionExitInfo2D = new();
        }

        [Tooltip("The collision handlers for this collsion events component.")]
        public CollisionHandler[] collisions = new CollisionHandler[]
        {
            new CollisionHandler()
        };

        private void Start() { /* for enabled/disabled */ }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!enabled) return;
            CollisionHandler handler = GetBestCallbackHandler(collision.relativeVelocity.magnitude, collision.gameObject);
            handler?.onCollisionEnter?.Invoke(collision.gameObject);
            handler?.onCollisionEnterTransform?.Invoke(collision.transform);
            handler?.onCollisionEnterCollider2D?.Invoke(collision.collider);
            if (collision.rigidbody)
                handler?.onCollisionEnterRigidbody2D?.Invoke(collision.rigidbody);
            handler?.onCollisionEnterInfo2D?.Invoke(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!enabled) return;
            CollisionHandler handler = GetBestCallbackHandler(collision.relativeVelocity.magnitude, collision.gameObject);
            handler?.onCollisionExit?.Invoke(collision.gameObject);
            handler?.onCollisionExitTransform?.Invoke(collision.transform);
            handler?.onCollisionExitCollider2D?.Invoke(collision.collider);
            if (collision.rigidbody)
                handler?.onCollisionExitRigidbody2D?.Invoke(collision.rigidbody);
            handler?.onCollisionExitInfo2D?.Invoke(collision);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!enabled) return;
            CollisionHandler handler = GetBestCallbackHandler(collision.impulse.magnitude / Time.fixedDeltaTime, collision.gameObject);
            handler?.onCollisionEnter?.Invoke(collision.gameObject);
            handler?.onCollisionEnterTransform?.Invoke(collision.transform);
            handler?.onCollisionEnterCollider?.Invoke(collision.collider);
            if (collision.rigidbody)
                handler?.onCollisionEnterRigidbody?.Invoke(collision.rigidbody);
            handler?.onCollisionEnterInfo?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!enabled) return;
            CollisionHandler handler = GetBestCallbackHandler(collision.impulse.magnitude / Time.fixedDeltaTime, collision.gameObject);
            handler?.onCollisionExit?.Invoke(collision.gameObject);
            handler?.onCollisionExitTransform?.Invoke(collision.transform);
            handler?.onCollisionExitCollider?.Invoke(collision.collider);
            if (collision.rigidbody)
                handler?.onCollisionExitRigidbody?.Invoke(collision.rigidbody);
            handler?.onCollisionExitInfo?.Invoke(collision);
        }

        private CollisionHandler GetBestCallbackHandler(float force, GameObject other)
        {
            GameObject attachedRb = other.TryGetComponent(out Collider threeD) && threeD.attachedRigidbody
                ? threeD.attachedRigidbody.gameObject
                : other.TryGetComponent(out Collider2D twoD) && twoD.attachedRigidbody
                    ? twoD.attachedRigidbody.gameObject
                    : null;

            float mass = attachedRb ? other.TryGetComponent(out Rigidbody rb) ? rb.mass : other.TryGetComponent(out Rigidbody2D rb2D) ? rb2D.mass : 0 : 0;

            CollisionHandler best = null;

            for (int i = 0; i < collisions.Length; i++)
            {
                CollisionHandler handler = collisions[i];

                if (force < handler.minForceThreshold)
                    continue;

                if (best != null && best.minForceThreshold > handler.minForceThreshold)
                    continue;

                GameObject compareTo = handler.checkAttachedRigidbody && attachedRb ? attachedRb : other;
                if (attachedRb && handler.minObjectMass > 0 && mass < handler.minObjectMass)
                    continue;
                if (handler.requiredLayers != ~Physics.AllLayers && !MVUtils.IsLayerInLayerMask(handler.requiredLayers, compareTo.layer))
                    continue;
                if (handler.requiredNames.Count > 0 && !handler.requiredNames.Contains(compareTo.name))
                    continue;
                if (handler.requiredTags.Count > 0 && !handler.requiredTags.Contains(compareTo.tag))
                    continue;

                best = handler;
            }

            return best;
        }
    }
}
