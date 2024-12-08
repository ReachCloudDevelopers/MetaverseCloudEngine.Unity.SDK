#if PLAYMAKER && METAVERSE_CLOUD_ENGINE && METAVERSE_CLOUD_ENGINE_INITIALIZED
using MetaverseCloudEngine.Unity.Editors;
using UnityEditor;
using UnityEngine;
using PlayMakerActions = HutongGames.PlayMakerEditor.Actions;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions.Editor
{
    [InitializeOnLoad]
    public class REACHExplorerPlayMakerCategoryIcon : MonoBehaviour
    {
        static REACHExplorerPlayMakerCategoryIcon()
        {
            PlayMakerActions.AddCategoryIcon(MetaverseConstants.ProductName, Icon);
        }

        public static Texture Icon => MetaverseEditorUtils.EditorIcon;
    }
}
#endif