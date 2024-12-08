using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    public class QualityLevelGraphicsOption : GraphicsOption
    {
        protected override void Initialize()
        {
            GenerateQualityLevelSuboptions();
        }

        public override void Apply()
        {
            if (Application.isMobilePlatform && MVUtils.IsVRCompatible())
                return;

            if (CurrentGraphicsSubOption.intValue == QualitySettings.GetQualityLevel())
                return;

            QualitySettings.SetQualityLevel(CurrentGraphicsSubOption.intValue, true);

#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
#endif
        }

        private void GenerateQualityLevelSuboptions()
        {
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                string qualityLevel = QualitySettings.names[i];
                GraphicsSubOption t = new GraphicsSubOption
                {
                    name = qualityLevel,
                    index = i,
                    intValue = i
                };
                SubOptionList.Add(t);
            }
        }

        public void SetCurrentsuboptionByValue(int v)
        {
            if (SubOptionList.Count == 0)
                return;

            foreach (GraphicsSubOption t in SubOptionList)
            {
                if (t.intValue == v)
                {
                    CurrentGraphicsSubOption = t;
                    CurrentSubOptionIndex = t.index;
                    UpdateSuboptionText();
                    return;
                }
            }

            SelectDefaultValue();
        }
    }
}

