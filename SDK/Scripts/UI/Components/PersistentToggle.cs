using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class PersistentToggle : MonoBehaviour
    {
        public string prefKey;
        public bool notify;
        public Toggle toggle;
        
        private void Awake()
        {
            if (toggle == null)
                toggle = GetComponent<Toggle>();
            
            if (notify)
                toggle.isOn = PlayerPrefs.GetInt(prefKey, toggle.isOn ? 1 : 0) == 1;
            else
                toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(prefKey, toggle.isOn ? 1 : 0) == 1);
            
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        private void OnToggleValueChanged(bool val)
        {
            PlayerPrefs.SetInt(prefKey, val ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}