using MetaverseCloudEngine.Unity.Physix.Components;

using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Receives raycast hit notifications sent by the <see cref="Raycast"/> component.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Raycast Receiver")]
    public class RaycastReceiver : TriInspectorMonoBehaviour
    {
        [Header("Filter")]
        [SerializeField] private List<string> validIdentifiers = new();

        [Header("(Optional) Spawn Object")]
        [SerializeField] private GameObject spawnAtHit;
        [SerializeField] private bool faceNormal = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onHit;

        /// <summary>
        /// Notify the receiver that it was hit by a raycast.
        /// </summary>
        /// <param name="sender">The sender of the ray.</param>
        /// <param name="hit">The raycast hit info.</param>
        public void NotifyHit(Raycast sender, RaycastHit hit)
        {
            if (!sender)
                return;

            if (validIdentifiers is { Count: > 0 } && !validIdentifiers.Contains(sender.Identifier))
                return;

            onHit?.Invoke();

            if (spawnAtHit)
            {
                Instantiate(spawnAtHit, hit.point, faceNormal ? Quaternion.LookRotation(hit.normal) : Quaternion.identity);
            }
        }
    }
}
