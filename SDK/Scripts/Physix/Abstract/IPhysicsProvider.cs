using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Abstract
{
    /// <summary>
    /// An interface that describes a physics system for a character.
    /// </summary>
    public interface IPhysicsProvider
    {
        /// <summary>
        /// Gets or sets the physical position.
        /// </summary>
        Vector3 Position { get; set; }
        /// <summary>
        /// Gets or sets the physical rotation.
        /// </summary>
        Quaternion Rotation { get; set; }
        /// <summary>
        /// Gets or sets the velocity of the physics object.
        /// </summary>
        Vector3 Velocity { get; set; }
        /// <summary>
        /// Whether to utilize fixed update or not.
        /// </summary>
        bool Interpolate { get; set; }
        /// <summary>
        /// Gets a value indicating whether the physics object is touching
        /// the ground.
        /// </summary>
        bool IsGrounded { get; }

        /// <summary>
        /// Gets or sets a value indicating whether launching is enabled.
        /// </summary>
        bool LaunchingEnabled { get; set; }
        /// <summary>
        /// Gets or sets the mass of the physics object.
        /// </summary>
        float Mass { get; set; }
        /// <summary>
        /// Gets or sets the kinematic state of the physics object.
        /// </summary>
        bool IsKinematic { get; set; }

        /// <summary>
        /// Launches the physics object.
        /// </summary>
        void Launch();
    }
}
