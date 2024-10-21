using TriInspectorMVCE;
using UnityEngine;

#if MV_XR_TOOLKIT_3
using InteractableSelectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode;
#else
using InteractableSelectMode = UnityEngine.XR.Interaction.Toolkit.InteractableSelectMode;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// Marks this object as something that can be climbed using the vr physics system, and possibly in the future.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Interactable/Climbable")]
    public class MetaverseClimbable : TriInspectorMonoBehaviour
    {
        [InfoBox("For more interaction options (and/or use events) add a 'Metaverse Interactable' component to this object.")]
        [Tooltip("The maximum distance that the player's hand can be from the initial grab point before the grab breaks.")]
        [SerializeField] private float grabBreakDistance = 0.5f;
        [Tooltip("If true, the player will collide with the object being grabbed. Otherwise it will pass through the player capsule.")]
        [SerializeField] private bool enablePlayerCollision = true;
        [Tooltip("If true, the player will be able to grab a 'flat surface' on this object. Otherwise, the player will only be able to grab the object from the side.")]
        [SerializeField] private bool allowFlatSurfaceGrabs = true;

        private void Awake()
        {
            if (!gameObject.TryGetComponent<Rigidbody>(out _))
            {
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                rb.hideFlags = HideFlags.HideInInspector;
                rb.isKinematic = true;
            }

            if (!gameObject.TryGetComponent(out MetaverseInteractable interactable))
            {
                interactable = gameObject.AddComponent<MetaverseInteractable>();
                interactable.hideFlags = HideFlags.HideInInspector;
            }
            
            interactable.IsClimbable = true;
            interactable.AllowFlatSurfaceClimbing = allowFlatSurfaceGrabs;
            interactable.CollideWithPlayer = enablePlayerCollision;
            interactable.PhysicsAttachmentBreakDistance = grabBreakDistance;
            interactable.selectMode = InteractableSelectMode.Multiple;
        }
    }
}