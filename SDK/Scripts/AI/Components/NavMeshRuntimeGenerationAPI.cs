using System;
using System.Collections.Generic;
using TriInspectorMVCE;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-int.MaxValue)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshRuntimeGenerationAPI : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class AgentSettings
        {
            public string name;
            public int id;
            public float radius;
            public float height;
            public float maxSlope;
            public float maxStepHeight;
            public float dropHeight;
            public float jumpDistance;
        }
        
        [Tooltip("The agent settings to use for building the NavMesh.")]
        [HideInInspector]
        [SerializeField] private AgentSettings agentSettings = new();

        /// <summary>
        /// The NavMeshSurface component to build.
        /// </summary>
        public NavMeshSurface Surface => GetComponent<NavMeshSurface>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) CollectAgentInfo();
        }
#endif

        /// <summary>
        /// Builds and instantiates this NavMesh surface.
        /// </summary>
        [Button("Build Nav Mesh")]
        public void BuildNavMesh()
        {
            if (Application.isEditor)
            {
                CollectAgentInfo();
            }
            
            //var sources = Surface.CollectSources();
            var sources = Surface.GetType().GetMethod("CollectSources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(Surface, null) as List<NavMeshBuildSource>;

            // Use unscaled bounds - this differs in behaviour from e.g. collider components.
            // But is similar to reflection probe - and since navmesh data has no scaling support - it is the right choice here.
            var surfaceBounds = new Bounds(Surface.center, Abs(Surface.size));
            if (Surface.collectObjects != CollectObjects.Volume)
            {
                //surfaceBounds = Surface.CalculateWorldBounds(sources);
                surfaceBounds = (Bounds)Surface.GetType().GetMethod("CalculateWorldBounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(Surface, new object[] { sources });
            }

            var data = NavMeshBuilder.BuildNavMeshData(
                new NavMeshBuildSettings
                {
                    agentTypeID = agentSettings.id,
                    agentRadius = agentSettings.radius,
                    agentHeight = agentSettings.height,
                    agentSlope = agentSettings.maxSlope,
                    agentClimb = agentSettings.maxStepHeight,
                    ledgeDropHeight = agentSettings.dropHeight,
                    maxJumpAcrossDistance = agentSettings.jumpDistance,
                    tileSize = Surface.tileSize,
                    overrideTileSize = Surface.overrideTileSize,
                    minRegionArea = Surface.minRegionArea,
                    buildHeightMesh = Surface.buildHeightMesh,
                    voxelSize = Surface.voxelSize,
                    overrideVoxelSize = Surface.overrideVoxelSize,
                },
                sources, 
                surfaceBounds, 
                transform.position, 
                transform.rotation);

            if (data)
            {
                data.name = gameObject.name;
                Surface.RemoveData();
                Surface.navMeshData = data;
                if (isActiveAndEnabled)
                    Surface.AddData();
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private void CollectAgentInfo()
        {
            if (!Surface)
                return;

            var settings = Surface.GetBuildSettings();
            agentSettings.id = settings.agentTypeID;
            agentSettings.radius = settings.agentRadius;
            agentSettings.height = settings.agentHeight;
            agentSettings.maxSlope = settings.agentSlope;
            agentSettings.maxStepHeight = settings.agentClimb;
            agentSettings.dropHeight = settings.ledgeDropHeight;
            agentSettings.jumpDistance = settings.maxJumpAcrossDistance;
        }
    }
}