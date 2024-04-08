using System;
using UnityEngine;
using MetaverseCloudEngine.Common.Enumerations;
using System.Collections.Generic;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    /// <summary>
    /// A helper component that allows for swapping of materials on a render-able mesh based on a platform.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [DefaultExecutionOrder(-int.MaxValue)]
    public class PlatformMaterial : MonoBehaviour
    {
        [Serializable]
        public class MaterialAssignment
        {
            [Tooltip("The index of the material to apply this material to.")]
            [Min(0)] public int materialIndex;
            [Tooltip("If this material will be applied to all other indices after the material at the 'Material Index'.")]
            public bool applyToAllIndices;
            public Material material;
        }

        [Tooltip("Whether to apply the materials on awake.")]
        public bool applyOnAwake = true;
        [Tooltip("The platforms that the materials will be applied on.")]
        public Platform platforms = Platform.All;
        [Tooltip("The materials to apply.")]
        public MaterialAssignment[] materials = Array.Empty<MaterialAssignment>();

        private static readonly List<Material> GetMaterialsNonAlloc = new();

        private void Awake()
        {
            if (applyOnAwake)
                ApplyMaterials();
        }

        private void Start() { /* for enabled/disabled flag */ }

        /// <summary>
        /// Applies the materials now.
        /// </summary>
        public void ApplyMaterials()
        {
            if (!enabled)
                return;

            if (!platforms.HasFlag(MetaverseProgram.GetCurrentPlatform(true)))
                return;

            var r = GetComponent<Renderer>();
            r.GetSharedMaterials(GetMaterialsNonAlloc);

            try
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    MaterialAssignment m = materials[i];
                    if (!m.material)
                        continue;

                    if (GetMaterialsNonAlloc.Count < m.materialIndex)
                    {
                        if (!m.applyToAllIndices)
                        {
                            GetMaterialsNonAlloc[m.materialIndex] = m.material;
                        }
                        else
                        {
                            for (int j = m.materialIndex; j < GetMaterialsNonAlloc.Count; j++)
                            {
                                GetMaterialsNonAlloc[j] = m.material;
                            }
                        }
                    }
                }

                r.sharedMaterials = GetMaterialsNonAlloc.ToArray();
            }
            finally
            {
                GetMaterialsNonAlloc.Clear();
            }
        }
    }
}
