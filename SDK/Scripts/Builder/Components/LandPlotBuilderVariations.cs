#pragma warning disable CS0067

using System;

using UnityEngine;
using UnityEngine.Events;

using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;

using System.Collections.Generic;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Builder.Components
{
    [RequireComponent(typeof(MetaPrefab))]
    [HideMonoScript]
    [Experimental]
    public partial class LandPlotBuilderVariations : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class VariationSet
        {
            public string name;
            public Sprite icon;
            public Variation[] variations;
        }

        [Serializable]
        public class Variation
        {
            public string name;
            public Sprite icon;
            [HideInInspector] public GameObject toggleObject;
            public List<GameObject> toggleObjects;

            [Header("Events")]
            public UnityEvent onSelected;
            public UnityEvent onDeselected;
        }

        public VariationSet[] variationSets;
        public event Action VariationsChanged;

        private void OnEnable()
        {
            Upgrade();
        }

        private void Upgrade()
        {
            foreach (var v in variationSets)
                foreach (var vr in v.variations)
                    if (vr.toggleObject)
                    {
                        vr.toggleObjects ??= new List<GameObject>();
                        vr.toggleObjects.Add(vr.toggleObject);
                        vr.toggleObject = null;
                    }
        }
    }
}