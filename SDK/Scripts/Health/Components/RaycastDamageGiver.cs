using UnityEngine;
using MetaverseCloudEngine.Unity.Physix.Components;
using TriInspectorMVCE;
using System;

namespace MetaverseCloudEngine.Unity.Health.Components
{
    [RequireComponent(typeof(Raycast))]
    [HideMonoScript]
    [Experimental]
    public class RaycastDamageGiver : DamageGiver
    {
        [SerializeField] private bool randomDamage = true;
        [SerializeField, Min(0)] private int minHitpoints = 1;
        [ShowIf(nameof(randomDamage))]
        [SerializeField, Min(0)] private int maxHitpoints = 5;

        private void OnValidate()
        {
            if (maxHitpoints < minHitpoints)
                maxHitpoints = minHitpoints;
        }

        public void RaycastDamage(RaycastHit hit)
        {
            var tr = hit.rigidbody ? hit.rigidbody.gameObject : hit.collider.gameObject;
            if (!tr || !tr.TryGetComponent(out HitPoints hp)) 
                return;
            hp.ApplyDamage(this, Array.Empty<object>());
        }

        public void RaycastDamage2D(RaycastHit2D hit)
        {
            var tr = hit.rigidbody ? hit.rigidbody.gameObject : hit.collider.gameObject;
            if (!tr || !tr.TryGetComponent(out HitPoints hp))
                return;
            hp.ApplyDamage(this, Array.Empty<object>());
        }

        public override bool TryGetDamage(HitPoints hp, object[] arguments, out int damage)
        {
            damage = randomDamage ? UnityEngine.Random.Range(minHitpoints, maxHitpoints) : minHitpoints;
            return true;
        }
    }
}