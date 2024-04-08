using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Serialization;
// ReSharper disable InconsistentNaming

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class BaseTheme : ScriptableObject
    {
        [FormerlySerializedAs("buttonDanger")] public Color ButtonDanger = new(1f, 54f / 255f, 65f / 255f, 1);
        [FormerlySerializedAs("buttonPrimary")] public Color ButtonPrimary = new(0, 116 / 255f, 217 / 255f, 1);
        [FormerlySerializedAs("buttonSecondary")] public Color ButtonSecondary = new(53f / 255f, 192f / 255f, 255f / 255f, 1);
        [FormerlySerializedAs("buttonInfo")] public Color ButtonInfo = new(57f / 255f, 204f / 255f, 204f / 255f, 1);

        [FormerlySerializedAs("primaryBackgroundDark")] public Color PrimaryBackgroundDark = new(20f / 255f, 20f / 255f, 20f / 255f, 1f);
        [FormerlySerializedAs("primaryBackgroundDarker")] public Color PrimaryBackgroundDarker = new(10f / 255f, 10f / 255f, 10f / 255f, 1f);
        [FormerlySerializedAs("primaryBackgroundLight")] public Color PrimaryBackgroundLight = new(221f / 255f, 221f / 255f, 221f / 255f, 1f);
        [FormerlySerializedAs("primaryBackgroundLighter")] public Color PrimaryBackgroundLighter = new(1f, 1f, 1f, 1f);

        [FormerlySerializedAs("secondaryBackgroundDark")] public Color SecondaryBackgroundDark = new(170f / 255f, 170f / 255f, 170f / 255f, 1f);
        [FormerlySerializedAs("secondaryBackgroundDarker")] public Color SecondaryBackgroundDarker = new(100f / 255f, 100f / 255f, 100f / 255f, 1f);
        [FormerlySerializedAs("secondaryBackgroundLight")] public Color SecondaryBackgroundLight = new(221f / 255f, 221f / 255f, 221f / 255f, 1f);
        [FormerlySerializedAs("secondaryBackgroundLighter")] public Color SecondaryBackgroundLighter = new(1f, 1f, 1f, 1f);

        [FormerlySerializedAs("textDark")] public Color TextDark = new(170f / 255f, 170f / 255f, 170f / 255f, 1f);
        [FormerlySerializedAs("textDarker")] public Color TextDarker = new(17f / 255f, 17f / 255f, 17f / 255f, 1f);
        [FormerlySerializedAs("textLight")] public Color TextLight = new(221f / 255f, 221f / 255f, 221f / 255f, 1f);
        [FormerlySerializedAs("textLighter")] public Color TextLighter = new(1f, 1f, 1f, 1f);
        
        [FormerlySerializedAs("SmallLogo")] 
        [FormerlySerializedAs("smallLogo")] 
        [FormerlySerializedAs("LargeLogo")] 
        [FormerlySerializedAs("largeLogo")] 
        public Texture2D Logo;

        private Sprite _sprite;

        private readonly Dictionary<ThemeColorType, Color> _colorCache = new();
        private readonly Dictionary<ThemeSpriteType, Sprite> _spriteCache = new();

        public static OrganizationThemeDto ToOrganizationTheme<T>(T unityTheme) where T : BaseTheme
        {
            if (unityTheme == null)
                return null;

            var themeDto = new OrganizationThemeDto();

            var unityColorProperties = unityTheme.GetType()
                .GetFields()
                .Where(x => x.FieldType == typeof(Color));

            var dtoColorProperties = themeDto.GetType()
                .GetProperties()
                .Where(x => x.PropertyType == typeof(int) && x.Name.EndsWith("Argb"))
                .ToDictionary(x => x.Name.Replace("Argb", string.Empty), y => y);
            
            foreach (var unityProp in unityColorProperties)
            {
                if (!dtoColorProperties.TryGetValue(unityProp.Name, out var dtoProp)) continue;
                var color = (Color)unityProp.GetValue(unityTheme);
                dtoProp.SetValue(themeDto, color.ToArgb());
            }

            return themeDto;
        }
        
        public static T FromOrganizationTheme<T>(OrganizationThemeDto themeDto) where T : BaseTheme
        {
            if (themeDto == null) 
                return CreateInstance<T>();
            
            var unityTheme = CreateInstance<T>();

            var unityColorProperties = unityTheme.GetType()
                .GetFields()
                .Where(x => x.FieldType == typeof(Color));

            var dtoColorProperties = themeDto.GetType()
                .GetProperties()
                .Where(x => x.PropertyType == typeof(int) && x.Name.EndsWith("Argb"))
                .ToDictionary(x => x.Name.Replace("Argb", string.Empty), y => y);
            
            foreach (var unityProp in unityColorProperties)
            {
                if (!dtoColorProperties.TryGetValue(unityProp.Name, out var dtoProp)) continue;
                var argb = (int)dtoProp.GetValue(themeDto, null);
                unityProp.SetValue(unityTheme, argb.ToColor());
            }
            
            return unityTheme;
        }
        
        private void Reset()
        {
            RepaintAll();
        }

        protected virtual void OnValidate()
        {
            RepaintAll();
        }

        public static void RepaintAll()
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                var graphics = MVUtils.FindObjectsOfTypeNonPrefabPooled<ThemeGraphic>(true);
                foreach (var g in graphics)
                    if (g) g.UpdateColor();

                var sprites = MVUtils.FindObjectsOfTypeNonPrefabPooled<ThemeSprite>(true);
                foreach (var s in sprites)
                    if (s) s.UpdateImage();

                var texts = MVUtils.FindObjectsOfTypeNonPrefabPooled<ThemeText>(true);
                foreach (var t in texts)
                    if (t) t.UpdateFont();
            });
        }

        public virtual Sprite GetSprite(ThemeSpriteType spriteType)
        {
            if (_spriteCache.TryGetValue(spriteType, out var sprite))
                return sprite;
            
            try
            {
                var t2d = (Texture2D) GetType().GetField(spriteType.ToString())?.GetValue(this);
                if (t2d != null && t2d && (_sprite == null || _sprite.name != t2d.name))
                    _sprite = Sprite.Create(t2d, new Rect(0, 0, t2d.width, t2d.height), Vector2.zero);
                _spriteCache.Add(spriteType, _sprite);
                return _sprite;
            }
            catch (MissingReferenceException)
            {
                return null; // Even though the texture is not null, sometimes the reference is lost.
            }
        }
        
        public virtual Color GetColor(ThemeColorType colorType)
        {
            if (_colorCache.TryGetValue(colorType, out var color))
                return color;
            color = (Color)GetType().GetField(colorType.ToString())!.GetValue(this);
            _colorCache.Add(colorType, color);
            return color;
        }
    }
}