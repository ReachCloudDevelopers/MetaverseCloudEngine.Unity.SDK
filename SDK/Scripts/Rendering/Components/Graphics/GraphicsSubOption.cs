using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [System.Serializable]
    public class GraphicsSubOption
    {
        [Tooltip("The name of the suboption that the user will see Ex 1920x1080, Off,On etc")]
        public string name;
        [Tooltip("The integer value corresponding to the name Ex Off = 0, On = 1")]
        public int intValue = 0;
        [Tooltip("The index value of this option in the suboption list, created in option class")]
        public int index;
        [Tooltip("The vector2 value specially for the resolution")]
        public Vector2 vector2Value = Vector2.zero;
    }
}

