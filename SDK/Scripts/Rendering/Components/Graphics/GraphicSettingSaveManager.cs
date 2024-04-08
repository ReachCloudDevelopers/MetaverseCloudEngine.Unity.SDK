using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    public class GraphicSettingSaveManager : MonoBehaviour
    {
        private const string ScreenWidthKey = "GraphicsSettings.ScreenWidth.v2";
        private const string ScreenHeightKey = "GraphicsSettings.ScreenHeight.v2";
        private const string ScreenModeKey = "GraphicsSettings.ScreenMode.v2";
        private const string QualityLevelKey = "GraphicsSettings.QualityLevel.v2";

        private GraphicSettingDataContainer currentDataContainer = new();

        private void Awake()
        {
            LoadData();
        }

        private void LoadData()
        {
            currentDataContainer.screenHeight = PlayerPrefs.GetInt(ScreenHeightKey, Screen.width);
            currentDataContainer.screenWidth = PlayerPrefs.GetInt(ScreenWidthKey, Screen.height);
            currentDataContainer.screenMode = PlayerPrefs.GetInt(ScreenModeKey, (int)Screen.fullScreenMode);
            currentDataContainer.qualityLevel = PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel());
        }

        public void Save(GraphicSettingDataContainer dataToSave)
        {
            PlayerPrefs.SetInt(ScreenWidthKey, dataToSave.screenWidth);
            PlayerPrefs.SetInt(ScreenHeightKey, dataToSave.screenHeight);
            PlayerPrefs.SetInt(ScreenModeKey, dataToSave.screenMode);
            PlayerPrefs.SetInt(QualityLevelKey, dataToSave.qualityLevel);
            PlayerPrefs.Save();

            currentDataContainer = dataToSave;
        }

        public void Load(out GraphicSettingDataContainer dataToLoad)
        {
            LoadData();
            dataToLoad = currentDataContainer;
        }
    }
}

