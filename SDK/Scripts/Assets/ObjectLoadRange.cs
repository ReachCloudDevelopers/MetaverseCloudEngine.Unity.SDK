using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets
{
    /// <summary>
    /// A helper class that helps with determining whether an object
    /// should be loaded or unloaded.
    /// </summary>
    [System.Serializable]
    public class ObjectLoadRange
    {
        /// <summary>
        /// Creates a new <see cref="ObjectLoadRange"/> with default
        /// values.
        /// </summary>
        public ObjectLoadRange()
        {
        }

        /// <summary>
        /// Creates a new <see cref="ObjectLoadRange"/> instance.
        /// </summary>
        /// <param name="loadDistance">The load distance to use.</param>
        /// <param name="unloadDistance">The unload distance to use.</param>
        public ObjectLoadRange(float loadDistance, float unloadDistance)
        {
            this.loadDistance = loadDistance;
            this.unloadDistance = unloadDistance;
        }
        
        [Tooltip("If this value is 0, the object will always load.")]
        [Min(0)] public float loadDistance;
        [Tooltip("If this value is 0, the object will never unload.")]
        [Min(0)] public float unloadDistance;

        /// <summary>
        /// Indicates whether the load requirements are met given the <paramref name="distance"/>.
        /// </summary>
        /// <param name="distance">The distance to the object..</param>
        /// <returns>A <see langword="true"/> value if the object should load.</returns>
        public bool ShouldLoad(float distance)
        {
            if (loadDistance <= 0) return true;
            return distance <= loadDistance;
        }

        /// <summary>
        /// Indicates whether the un-load requirements are met given the <paramref name="distance"/>.
        /// </summary>
        /// <param name="distance">The distance to the object.</param>
        /// <returns>A <see langword="true"/> value if the object should un-load.</returns>
        public bool ShouldUnload(float distance)
        {
            if (unloadDistance <= 0) return false;
            return distance > Mathf.Max(unloadDistance, loadDistance);
        }

        /// <summary>
        /// EDITOR ONLY: Called with <see cref="MonoBehaviour"/>.OnValidate().
        /// </summary>
        public void Validate()
        {
            unloadDistance = Mathf.Max(loadDistance, unloadDistance);
        }

        /// <summary>
        /// EDITOR ONLY: Draws unity gizmos in the scene view.
        /// </summary>
        /// <param name="transform">The source transform object.</param>
        public void DrawGizmos(Transform transform)
        {
            Vector3 pos = transform.position;
            Color oldColor = Gizmos.color;
            
            if (loadDistance > 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(pos, loadDistance);
            }

            if (unloadDistance > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pos, unloadDistance);
            }
            
            Gizmos.color = oldColor;
        }
    }
}