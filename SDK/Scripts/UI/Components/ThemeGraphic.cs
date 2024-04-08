using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class ThemeGraphic : MonoBehaviour
    {
        [SerializeField] private ThemeColorType colorType;
        [SerializeField] private Theme overrideTheme;
        [SerializeField, HideInInspector] private Theme theme;

        public ThemeColorType ColorType
        {
            get => colorType;
            set => colorType = value;
        }

        public Theme Theme => overrideTheme
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            ? overrideTheme : MetaverseInternalResources.Instance.defaultTheme
#endif
            ;

        private void OnEnable()
        {
            UpdateColor();
        }

        private void OnValidate()
        {
            UpdateColor();
        }

        public void UpdateColor()
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this)
                    return;
                
                if (Theme == null)
                    return;

                if (TryGetComponent(out Graphic graphic) && graphic.color != Theme.GetColor(ColorType))
                    graphic.color = Theme.GetColor(ColorType);
            });
        }
    }
}