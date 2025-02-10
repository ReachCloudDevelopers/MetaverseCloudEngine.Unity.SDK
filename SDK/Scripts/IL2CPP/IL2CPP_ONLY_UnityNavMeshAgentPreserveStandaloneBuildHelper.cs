#if MV_UNITY_AI_NAV

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting;

namespace MetaverseCloudEngine.Unity.Internal.IL2CPP
{
    /// <summary>
    /// Helps preserve the unity navmesh agent properties in standalone player builds.
    /// </summary>
    [Preserve]
    [AddComponentMenu("")]
    internal class IL2CPP_ONLY_UnityNavMeshAgentPreserveStandaloneBuildHelper : MonoBehaviour
    {
        private void Start()
        {
            try
            {
                NavMeshAgent navMeshAgent = GetComponent<NavMeshAgent>();
                var dest = navMeshAgent.destination = new Vector3(0, 0, 0);
                navMeshAgent.SetDestination(Vector3.zero);
                navMeshAgent.velocity = Vector3.zero;
                var angular = navMeshAgent.angularSpeed = 0;
                var accel = navMeshAgent.acceleration = 0;
                var aitpTrav = navMeshAgent.autoTraverseOffMeshLink = false;
                navMeshAgent.autoRepath = false;
                navMeshAgent.autoBraking = false;
                var height = navMeshAgent.height = 0;
                var baseOffset = navMeshAgent.baseOffset = 0;
                var rad = navMeshAgent.radius = 0;
                var avoidance = navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                var avoidancePrio = navMeshAgent.avoidancePriority = 0;
                var stoppingDist = navMeshAgent.stoppingDistance = 0;
                var r = navMeshAgent.remainingDistance;
                var p = navMeshAgent.path;
                var c = navMeshAgent.pathStatus;
                var s = navMeshAgent.isStopped;
                var w = navMeshAgent.isOnNavMesh;
                var a = navMeshAgent.hasPath;
                var b = navMeshAgent.pathPending;
                var d = navMeshAgent.updatePosition;
                var e = navMeshAgent.updateRotation;
                var f = navMeshAgent.nextPosition;
                var g = navMeshAgent.agentTypeID;
                var v = navMeshAgent.velocity;
                var ab= navMeshAgent.autoBraking;
                var ac = navMeshAgent.autoRepath;
                var steering = navMeshAgent.steeringTarget;
            }
            catch
            {
                /* ignored */
            }
        }
    }
}
#endif