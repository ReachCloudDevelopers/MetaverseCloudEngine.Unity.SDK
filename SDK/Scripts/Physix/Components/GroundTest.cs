using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// Performs a raycast to test for a ground surface.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Ground Test (Raycast)")]
    public class GroundTest : TriInspectorMonoBehaviour
    {
        [System.Serializable]
        public class SurfaceCheckSettings
        {
            [Tooltip("The object that should be moved when a surface is found.")]
            public Transform objectToMove;
            [Tooltip("Whether to move the transform position of this object to the surface.")]
            public bool setPosition = true;
            [Tooltip("The offset to use when setting the position.")]
            public Vector3 setPositionOffset;
            [Tooltip("Whether to align the rotation to the found surface.")]
            public bool setRotation = false;

            [Header("Ray Settings")]
            [Tooltip("The distance above the object to move's position to start the check.")]
            public float heightOffset = 100000;
            [Tooltip("The object space to use for determining the down direction. If self, uses the parent down, otherwise the local down.")]
            public Space checkSpace = Space.World;
            [Tooltip("The distance to check for the surface.")]
            public float checkDistance = Mathf.Infinity;
            [Tooltip("The layers that are considered valid surfaces.")]
            public LayerMask checkLayers = Physics.DefaultRaycastLayers;
            [Tooltip("Ignore all game objects with these tags, including their children.")]
            public string[] ignoreTags = new string[] { "Player" };
            [Tooltip("The check's interaction with triggers. Default is 'Ignore'.")]
            public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        }

        [System.Serializable]
        public class SurfaceCheckEvents
        {
            [Header("Success")]
            public UnityEvent<Vector3> onSurfacePosition = new();
            public UnityEvent<Vector3> onSurfaceNormal = new();
            public UnityEvent<Quaternion> onSurfaceRotation = new();
            public UnityEvent<GameObject> onSurfaceGameObject = new();

            [Header("Failed")]
            public UnityEvent onSurfaceNotFound;
        }

        public bool testOnStart = true;
        public SurfaceCheckSettings settings = new();
        public SurfaceCheckEvents events = new();

        private void Start()
        {
            if (testOnStart)
                Test();
        }

        private void Reset()
        {
            settings.objectToMove = transform;
        }

        public void Test()
        {
            if (!settings.objectToMove)
                return;

            var direction = GetDownDirection();
            var ray = new Ray(settings.objectToMove.position + (-direction * settings.heightOffset), direction);
            var hits = Physics.RaycastAll(ray, settings.checkDistance, settings.checkLayers, settings.triggerInteraction);
            RaycastHit closestHit = default;
            var hitDistance = Mathf.Infinity;
            var found = false;

            foreach (var hit in hits)
            {
                if (settings.ignoreTags.Contains(hit.collider.tag) || hit.collider.GetComponentsInParent<Transform>().Any(x => settings.ignoreTags.Contains(tag)))
                    continue;

                var distance = Vector3.Distance(hit.point, settings.objectToMove.position);
                if (distance < hitDistance)
                {
                    closestHit = hit;
                    hitDistance = distance;
                    found = true;
                }
            }

            if (found)
            {
                var rotation = Quaternion.FromToRotation(-direction, closestHit.normal);
                if (settings.setPosition) settings.objectToMove.position = closestHit.point + (rotation * settings.setPositionOffset);
                if (settings.setRotation) settings.objectToMove.up = closestHit.normal;
                events.onSurfaceGameObject?.Invoke(closestHit.collider.gameObject);
                events.onSurfacePosition?.Invoke(closestHit.point);
                events.onSurfaceRotation?.Invoke(rotation);
                events.onSurfaceNormal?.Invoke(closestHit.normal);
            }
            else
            {
                events.onSurfaceNotFound?.Invoke();
            }
        }

        private Vector3 GetDownDirection()
        {
            switch(settings.checkSpace)
            {
                case Space.World:
                    return Vector3.down;
                case Space.Self:
                    return settings.objectToMove.parent ? -settings.objectToMove.parent.up : -settings.objectToMove.up;
            }

            return Vector3.down;
        }
    }
}
