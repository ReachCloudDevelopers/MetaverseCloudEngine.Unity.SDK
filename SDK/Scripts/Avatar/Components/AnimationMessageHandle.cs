using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [CreateAssetMenu(menuName = MetaverseConstants.MenuItems.MenuRootPath + "Avatar/Animation Message Handle")]
    public class AnimationMessageHandle : ScriptableObject
    {
        public float floatValue;
        public int intValue;
        public string stringValue;
        public Object objectValue;
        public bool boolValue;
    }
}
