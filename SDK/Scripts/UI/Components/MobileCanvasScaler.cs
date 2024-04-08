using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(CanvasScaler))]
    public class MobileCanvasScaler : MonoBehaviour
    {
        public float multiplier = 0.8f;

        private void Awake()
        {
            if (Application.isMobilePlatform)
                GetComponent<CanvasScaler>().referenceResolution *= multiplier;
        }
    }
}