using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class MasterButtonClickListener : MonoBehaviour
    {
        public UnityEvent onClick;

        private void Awake()
        {
            foreach (Button button in GetComponentsInChildren<Button>())
            {
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
