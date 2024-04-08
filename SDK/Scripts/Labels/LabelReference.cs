using UnityEngine;

namespace MetaverseCloudEngine.Unity.Labels
{
    [CreateAssetMenu(menuName = MetaverseConstants.MenuItems.MenuRootPath + "Misc/Label")]
    public class LabelReference : ScriptableObject
    {
        public Label label;
    }
}
