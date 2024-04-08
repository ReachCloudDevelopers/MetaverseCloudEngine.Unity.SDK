using MetaverseCloudEngine.Unity.Async;
using TMPro;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class ThemeText : MonoBehaviour
    {
        [SerializeField] private ThemeFontIndex index;
        [SerializeField] private Theme overrideTheme;
        [SerializeField, HideInInspector] private Theme theme;

        public ThemeFontIndex Index
        {
            get => index;
            set => index = value;
        }

        public Theme Theme => overrideTheme
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            ? overrideTheme : MetaverseInternalResources.Instance.defaultTheme
#endif
            ;

        private void OnEnable()
        {
            UpdateFont();
        }

        private void OnValidate()
        {
            UpdateFont();
        }

        public void UpdateFont()
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this)
                    return;
                
                if (Theme == null)
                    return;

                var font = Index switch
                {
                    ThemeFontIndex.Primary => Theme.PrimaryFont,
                    ThemeFontIndex.Secondary => Theme.SecondaryFont,
                    ThemeFontIndex.Tertiary => Theme.TertiaryFont,
                    _ => GetComponent<TMP_Text>().font
                };

                if (font != null && TryGetComponent(out TMP_Text text))
                    text.font = font;
            });
        }
    }
}