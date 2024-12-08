using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [RequireComponent(typeof(GraphicSettingSaveManager))]
    public class GraphicMenuManager : MonoBehaviour
    {
        [FormerlySerializedAs("resolutionOption")] public ResolutionGraphicsOption resolutionGraphicsOption;
        [FormerlySerializedAs("screenmodeOption")] public ScreenmodeGraphicsOption screenmodeGraphicsOption;
        [FormerlySerializedAs("qualityLevelOption")] public QualityLevelGraphicsOption qualityLevelGraphicsOption;

        private readonly GraphicSettingDataContainer dataToSave = new();
        private GraphicSettingDataContainer dataToLoad = new();
        private GraphicSettingSaveManager graphicSettingSaveManager;

        private void Awake()
        {
            graphicSettingSaveManager = GetComponents<GraphicSettingSaveManager>().FirstOrDefault();
        }

        private void Start()
        {
            Load();
        }

        public void OnApplyButtonPress()
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            resolutionGraphicsOption.Apply();
            screenmodeGraphicsOption.Apply();
            qualityLevelGraphicsOption.Apply();
            Save();
            
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog(
                "Graphic Settings",
                "Graphic settings applied successfully!", 
                "Ok");
#endif
        }

        public void Save()
        {
            if (resolutionGraphicsOption.CurrentGraphicsSubOption != null)
            {
                dataToSave.screenHeight = (int)resolutionGraphicsOption.CurrentGraphicsSubOption.vector2Value.y;
                dataToSave.screenWidth = (int)resolutionGraphicsOption.CurrentGraphicsSubOption.vector2Value.x;
            }
            else
            {
                dataToSave.screenHeight = Screen.height;
                dataToSave.screenWidth = Screen.width;
            }

            if (screenmodeGraphicsOption.CurrentGraphicsSubOption != null)
                dataToSave.screenMode = screenmodeGraphicsOption.CurrentGraphicsSubOption.intValue;
            else
                dataToSave.screenMode = (int)Screen.fullScreenMode;

            if (qualityLevelGraphicsOption.CurrentGraphicsSubOption != null)
                dataToSave.qualityLevel = qualityLevelGraphicsOption.CurrentGraphicsSubOption.intValue;
            else
                dataToSave.qualityLevel = QualitySettings.GetQualityLevel();

            graphicSettingSaveManager.Save(dataToSave);
        }

        public void Load()
        {
            graphicSettingSaveManager.Load(out dataToLoad);
            UpdateUIFromLoadedData();
        }

        private void UpdateUIFromLoadedData()
        {
            resolutionGraphicsOption.SetCurrentsuboptionByValue(new Vector2(dataToLoad.screenWidth, dataToLoad.screenHeight));
            screenmodeGraphicsOption.SetCurrentsuboptionByValue(dataToLoad.screenMode);
            qualityLevelGraphicsOption.SetCurrentsuboptionByValue(dataToLoad.qualityLevel);
        }
    }
}

