using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    public class ClampTransformMagnitude : TriInspectorMonoBehaviour
    {
        [Min(0)]
        [SerializeField] private float maxMagnitude = 1f;
        [LabelText("Origin (Optional)")]
        [SerializeField] private Transform origin;
        [SerializeField] private bool clampInUpdate = true;
        [SerializeField] private bool clampInLateUpdate;
        [SerializeField] private bool clampInFixedUpdate;

        private void Update()
        {
            if (clampInUpdate)
                Clamp();
        }
        
        private void LateUpdate()
        {
            if (clampInLateUpdate)
                Clamp();
        }
        
        private void FixedUpdate()
        {
            if (clampInFixedUpdate)
                Clamp();
        }

        /// <summary>
        /// Clamps the position of the transform to the max magnitude.
        /// </summary>
        public void Clamp()
        {
            if (!origin)
            {
                if (transform.position.magnitude > maxMagnitude)
                    transform.position = transform.position.normalized * maxMagnitude;
            }
            else
            {
                if ((transform.position - origin.position).magnitude > maxMagnitude)
                    transform.position = origin.position + (transform.position - origin.position).normalized * maxMagnitude;
            }
        }
    }
}
