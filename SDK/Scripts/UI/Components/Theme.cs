using TMPro;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [CreateAssetMenu(fileName = "New Theme", menuName = MetaverseConstants.MenuItems.MenuRootPath + "UI/Theme")]
    public class Theme : BaseTheme
    {
        [Title("Font")]
        [FormerlySerializedAs("primaryFont")] public TMP_FontAsset PrimaryFont;
        [FormerlySerializedAs("secondaryFont")] public TMP_FontAsset SecondaryFont;
        [FormerlySerializedAs("tertiaryFont")] public TMP_FontAsset TertiaryFont;

        [Title("Override")]
        [FormerlySerializedAs("Override")] public BaseTheme @override;

        protected override void OnValidate()
        {
            base.OnValidate();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += () =>
            {
                if (!Application.isPlaying)
                {
                    ClearOverrideTheme();
                }
            };
#endif
        }

        public void SetOverrideTheme(BaseTheme theme)
        {
            if (@override != theme)
            {
                @override = theme;
                RepaintAll();

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
#endif
            }
        }

        public void ClearOverrideTheme() => SetOverrideTheme(null);

        public override Sprite GetSprite(ThemeSpriteType spriteType) => @override ? @override.GetSprite(spriteType) ?? base.GetSprite(spriteType) : base.GetSprite(spriteType);

        public override Color GetColor(ThemeColorType colorType) => @override ? @override.GetColor(colorType) : base.GetColor(colorType);
    }
}