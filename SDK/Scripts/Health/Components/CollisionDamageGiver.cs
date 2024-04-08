using System.Linq;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Health.Components
{
    [DisallowMultipleComponent]
    [HideMonoScript]
    [Experimental]
    public class CollisionDamageGiver : DamageGiver
    {
        [Header("Hit Points")]
        [SerializeField] private bool randomDamage = true;
        [SerializeField, Min(0)] private int minHitpoints = 1;
        [ShowIf(nameof(randomDamage))]
        [SerializeField, Min(0)] private int maxHitpoints = 5;
        [HideIf(nameof(randomDamage))]
        [SerializeField, Min(0)] private float minImpactForce = 5;
        [HideIf(nameof(randomDamage))]
        [SerializeField, Min(0)] private float maxImpactForce = 10;

        private static readonly object[] DamageArgs = new object[1];

        private void OnValidate()
        {
            if (minHitpoints > maxHitpoints)
                minHitpoints = maxHitpoints;
            if (minImpactForce > maxImpactForce)
                minImpactForce = maxImpactForce;
        }

        public void ApplyCollisionDamage(Collision col)
        {
            var tr = col.rigidbody ? col.rigidbody.gameObject : col.gameObject;
            if (!tr || !tr.TryGetComponent(out HitPoints hp))
                return;
            DamageArgs[0] = col.impulse;
            hp.ApplyDamage(this, DamageArgs);
        }

        public void ApplyCollisionDamage2D(Collision2D col)
        {
            var tr = col.rigidbody ? col.rigidbody.gameObject : col.gameObject;
            if (!tr || !tr.TryGetComponent(out HitPoints hp))
                return;
            var totalImpulse = col.contacts.Aggregate(Vector3.zero, (current, contact) => current + (Vector3)contact.normal * contact.normalImpulse);
            DamageArgs[0] = totalImpulse;
            hp.ApplyDamage(this, DamageArgs);
        }

        public override bool TryGetDamage(HitPoints hp, object[] arguments, out int damage)
        {
            // TODO: May want to do a lot more to check the authenticity of the collision
            // event in the future.

            damage = 0;
            if (arguments is not { Length: 1 } || arguments[0] is not Vector3 impactForce)
                return false;

            if (!randomDamage)
            {
                if (impactForce.magnitude < minImpactForce)
                    return false;

                var d = Mathf.InverseLerp(minImpactForce, maxImpactForce, impactForce.magnitude);
                damage = (int)Mathf.Lerp(d, minHitpoints, maxHitpoints);
            }
            else
            {
                damage = Random.Range(minHitpoints, maxHitpoints);
            }

            return true;
        }
    }
}
