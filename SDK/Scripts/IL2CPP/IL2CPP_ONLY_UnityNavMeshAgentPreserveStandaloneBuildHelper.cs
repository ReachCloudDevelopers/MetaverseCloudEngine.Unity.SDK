#if MV_UNITY_AI_NAV

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting;

namespace MetaverseCloudEngine.Unity.Internal.IL2CPP
{
    /// <summary>
    /// Ensures that all NavMeshAgent properties are preserved in standalone builds by reading and writing them.
    /// </summary>
    [Preserve]
    [AddComponentMenu("")]
    internal class NavMeshAgentPreserver : MonoBehaviour
    {
        private void Start()
        {
            PreserveAgentProperties();
        }

        /// <summary>
        /// Writes dummy values to and then reads from all NavMeshAgent properties to force IL2CPP to preserve them.
        /// </summary>
        private void PreserveAgentProperties()
        {
            // Attempt to get the NavMeshAgent component on this GameObject.
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                return;
            }

            try
            {
                // --- Write Operations: assign default/dummy values ---

                // Set destination via property and method.
                agent.destination = Vector3.zero;
                agent.SetDestination(Vector3.zero);

                // Write velocity, angular speed, and acceleration.
                agent.velocity = Vector3.zero;
                agent.angularSpeed = 0f;
                agent.acceleration = 0f;

                // Configure path and movement behavior.
                agent.autoTraverseOffMeshLink = false;
                agent.autoRepath = false;
                agent.autoBraking = false;
                agent.updatePosition = false;
                agent.updateRotation = false;

                // Set collider-like dimensions.
                agent.height = 0f;
                agent.baseOffset = 0f;
                agent.radius = 0f;

                // Set obstacle avoidance settings.
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                agent.avoidancePriority = 0;

                // Set path stopping distance.
                agent.stoppingDistance = 0f;

                // --- Read Operations: access the properties to force preservation ---

                // Read destination and velocity.
                Vector3 dummyDestination = agent.destination;
                Vector3 dummyVelocity = agent.velocity;

                // Read angular speed and acceleration.
                float dummyAngularSpeed = agent.angularSpeed;
                float dummyAcceleration = agent.acceleration;

                // Read path behavior settings.
                bool dummyAutoTraverse = agent.autoTraverseOffMeshLink;
                bool dummyAutoRepath = agent.autoRepath;
                bool dummyAutoBraking = agent.autoBraking;

                // Read dimension properties.
                float dummyHeight = agent.height;
                float dummyBaseOffset = agent.baseOffset;
                float dummyRadius = agent.radius;

                // Read obstacle avoidance properties.
                ObstacleAvoidanceType dummyObstacleAvoidance = agent.obstacleAvoidanceType;
                int dummyAvoidancePriority = agent.avoidancePriority;

                // Read stopping distance.
                float dummyStoppingDistance = agent.stoppingDistance;

                // Read additional readonly properties.
                float dummyRemainingDistance = agent.remainingDistance;
                NavMeshPath dummyPath = agent.path;
                NavMeshPathStatus dummyPathStatus = agent.pathStatus;
                bool dummyIsStopped = agent.isStopped;
                bool dummyIsOnNavMesh = agent.isOnNavMesh;
                bool dummyHasPath = agent.hasPath;
                bool dummyPathPending = agent.pathPending;
                Vector3 dummyNextPosition = agent.nextPosition;
                int dummyAgentTypeID = agent.agentTypeID;
                Vector3 dummySteeringTarget = agent.steeringTarget;
            }
            catch
            {
                // Swallow any exceptions; this helper is only for preserving IL2CPP metadata.
            }
        }
    }
}

#endif
