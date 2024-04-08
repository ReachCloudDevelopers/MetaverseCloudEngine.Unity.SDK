using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    public class ResolutionGraphicsOption : GraphicsOption
    {
        protected override void Initialize()
        {
            GenerateResolutionSubOptions();
        }

        public override void Apply()
        {
            if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer) return;
            Screen.SetResolution((int)CurrentGraphicsSubOption.vector2Value.x, (int)CurrentGraphicsSubOption.vector2Value.y, Screen.fullScreenMode);
        }

        private void GenerateResolutionSubOptions()
        {
            SubOptionList.Clear();

            Vector2[] resolutions = Screen.resolutions.Select(x => new Vector2(x.width, x.height)).Distinct().ToArray();
            for (int i = 0; i < resolutions.Length; i++)
            {
                Vector2 r = resolutions[i];
                GraphicsSubOption t = new GraphicsSubOption
                {
                    name = r.x.ToString() + "x" + r.y.ToString(),
                    vector2Value = new Vector2(r.x, r.y),
                    index = i
                };

                SubOptionList.Add(t);
            }
        }

        public void SetCurrentsuboptionByValue(Vector2 v)
        {
            if (SubOptionList.Count == 0)
                return;

            foreach (GraphicsSubOption t in SubOptionList)
            {
                if (t.vector2Value == v)
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

