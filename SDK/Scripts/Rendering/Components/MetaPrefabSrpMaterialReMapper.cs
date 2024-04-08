using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [RequireComponent(typeof(MetaPrefab))]
    [HideMonoScript]
    [Experimental]
    public class MetaPrefabSrpMaterialReMapper : TriInspectorMonoBehaviour
    {
        [Serializable]
        [DeclareHorizontalGroup("Materials")]
        public class FromToMaterial
        {
            [Group("Materials")]
            [HideLabel]
            public Material from;
            [Group("Materials")]
            [LabelText("Standard <-> URP")]
            public Material to;
        }
        
        [InfoBox("When URP is active, this maps the standard materials to the corresponding URP materials.")]
        public FromToMaterial[] materials;
        
        private Dictionary<Material, Material> _materialMap;
        private Dictionary<Material, Material> _reverseMaterialMap;

        public Dictionary<Material, Material> GetMap(bool reverse = false)
        {
            _materialMap ??= materials.Where(x => x.from).ToDictionary(x => x.from, x => x.to);
            _reverseMaterialMap ??= _materialMap.Where(x => x.Value).ToDictionary(x => x.Value, x => x.Key);
            return reverse ? _materialMap : _reverseMaterialMap;
        }

        public void SetMap(Dictionary<Material,Material> map)
        {
            _materialMap = map;
            materials = map.Select(x => new FromToMaterial {from = x.Key, to = x.Value}).ToArray();
        }

        [Button("Add Materials from Children")]
        public void AddMaterialsFromChildren()
        {
            var sharedMaterialsInChildren = GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Distinct()
                .Where(x => materials == null || materials.All(y => y.from != x));
            
            materials = (materials ?? Array.Empty<FromToMaterial>())
                .Concat(sharedMaterialsInChildren.Select(x => new FromToMaterial {from = x}))
                .ToArray();
        }
    }
}