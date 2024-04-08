using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(Image))]
    public class ThemeSprite : MonoBehaviour
    {
        [SerializeField] private ThemeSpriteType imageType;
        [SerializeField] private Theme overrideTheme;
        [SerializeField, HideInInspector] private Theme theme;

        public ThemeSpriteType ImageType
        {
            get => imageType;
            set => imageType = value;
        }

        public Theme Theme => overrideTheme
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            ? overrideTheme : MetaverseInternalResources.Instance.defaultTheme
#endif
            ;

        private void OnEnable()
        {
            UpdateImage();
        }

        private void OnValidate()
        {
            UpdateImage();
        }

        public void UpdateImage()
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this)
                    return;
                
                if (!Theme)
                    return;

                var sprite = Theme.GetSprite(ImageType);
                if (sprite && TryGetComponent(out Image img) && img.overrideSprite != sprite)
                {
                    img.overrideSprite = sprite;
                }
            });
        }
    }
}