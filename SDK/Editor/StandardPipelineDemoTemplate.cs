using JetBrains.Annotations;
using UnityEditor.SceneTemplate;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Editors
{
    [UsedImplicitly]
    public class StandardPipelineDemoTemplate : ISceneTemplatePipeline
    {
        [UsedImplicitly]
        public StandardPipelineDemoTemplate()
        {
        }
    
        public virtual bool IsValidTemplateForInstantiation(SceneTemplateAsset sceneTemplateAsset) 
            => GraphicsSettings.renderPipelineAsset == null;

        public virtual void BeforeTemplateInstantiation(SceneTemplateAsset sceneTemplateAsset, bool isAdditive, string sceneName)
        {
        }

        public virtual void AfterTemplateInstantiation(SceneTemplateAsset sceneTemplateAsset, Scene scene, bool isAdditive, string sceneName) 
            => GraphicsSettings.renderPipelineAsset = null;
    }
}
