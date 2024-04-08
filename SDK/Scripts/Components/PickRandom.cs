using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class PickRandom : TriInspectorMonoBehaviour
    {
        [SerializeField] private bool pickOnStart = true;
        [SerializeField] private bool ignoreDisabled = true;
        [SerializeField] private bool findPickables;
        [HideIf(nameof(findPickables))]
        [SerializeField] private List<RandomlyPickable> pickables = new();

        private bool _hasStarted;

        private void Start()
        {
            _hasStarted = true;
            if (pickOnStart)
                Pick();
        }

        public void Pick()
        {
            if (!_hasStarted && pickOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            RandomlyPickable pickable = GetRandom();
            if (pickable)
                pickable.TriggerPicked();
        }

        public void Add(RandomlyPickable pickable)
        {
            pickables.Add(pickable);
        }

        public void Remove(RandomlyPickable remove)
        {
            pickables.Remove(remove);
        }

        private RandomlyPickable GetRandom()
        {
            if (findPickables)
                pickables = GetComponentsInChildren<RandomlyPickable>(true).ToList();

            float totalWeight = pickables.Sum(x => x ? x.Weight : 0);
            float itemWeightIndex = UnityEngine.Random.value * totalWeight;
            float weightIndex = 0f;

            foreach (RandomlyPickable item in pickables)
            {
                if (!item || (ignoreDisabled && !item.isActiveAndEnabled))
                    continue;

                weightIndex += item.Weight;
                if (weightIndex >= itemWeightIndex)
                    return item;

            }

            return null;

        }
    }
}