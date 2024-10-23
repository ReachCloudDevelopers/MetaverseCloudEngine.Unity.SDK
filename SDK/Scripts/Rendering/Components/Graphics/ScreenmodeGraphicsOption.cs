using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    public class ScreenmodeGraphicsOption : GraphicsOption
    {
        protected override void Initialize()
        {
            GenerateScreenModeSubOptions();
        }

        private void GenerateScreenModeSubOptions()
        {
            SubOptionList.Clear();

            FullScreenMode[] values = (FullScreenMode[])Enum.GetValues(typeof(FullScreenMode));
            for (int i = 0; i < values.Length; i++)
            {
                FullScreenMode f = values[i];
                GraphicsSubOption t = new GraphicsSubOption
                {
                    name = f.ToString().CamelCaseToSpaces(),
                    intValue = (int)f,
                    index = i
                };
                SubOptionList.Add(t);
            }
        }

        public override void Apply()
        {
            if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer) return;
            if (Screen.fullScreenMode != (FullScreenMode)CurrentGraphicsSubOption.intValue)
                Screen.fullScreenMode = (FullScreenMode)CurrentGraphicsSubOption.intValue;
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

