#if MV_UNITY_AI_NAV

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TriInspectorMVCE;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-int.MaxValue)]
    [RequireComponent(typeof(NavMeshSurface))]
    [HideMonoScript]
    public class NavMeshRuntimeGenerationAPI : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class AgentSettings
        {
            public int id;
            public float radius;
            public float height;
            public float maxSlope;
            public float maxStepHeight;
            public float dropHeight;
            public float jumpDistance;
        }
        
        [InfoBox("This component is required in order to preserve custom settings for Agent Types when building the NavMesh at runtime.")]
        [Tooltip("The agent settings to use for building the NavMesh.")]
        [ReadOnly]
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
        [UsedImplicitly]
        public void BuildNavMesh()
        {
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

            var buildSettings = Surface.GetBuildSettings();
            buildSettings.agentTypeID = agentSettings.id;
            buildSettings.agentRadius = agentSettings.radius;
            buildSettings.agentHeight = agentSettings.height;
            buildSettings.agentSlope = agentSettings.maxSlope;
            buildSettings.agentClimb = agentSettings.maxStepHeight;
            buildSettings.ledgeDropHeight = agentSettings.dropHeight;
            buildSettings.maxJumpAcrossDistance = agentSettings.jumpDistance;

            var data = NavMeshBuilder.BuildNavMeshData(
                buildSettings,
                sources, 
                surfaceBounds, 
                Surface.transform.position, 
                Surface.transform.rotation);

            if (data == null) 
                return;
            
            data.name = gameObject.name;
            Surface.RemoveData();
            Surface.navMeshData = data;
            if (isActiveAndEnabled)
                Surface.AddData();
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

#endif