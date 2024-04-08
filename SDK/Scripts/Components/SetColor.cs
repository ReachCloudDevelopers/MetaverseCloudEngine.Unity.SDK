using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper class to set color values.
    /// </summary>
    public class SetColor : MonoBehaviour
    {
        [Tooltip("The color to set.")]
        public Color color = Color.white;
        [Tooltip("Whether to set the color on Start().")]
        public bool setOnStart;

        [Tooltip("Invoked when the color is set.")]
        public UnityEvent<Color> onColor;
        [Tooltip("Invoked when the color is set. This is the 32 bit version of the color value.")]
        public UnityEvent<Color32> onColor32;
        [Tooltip("Invoked when the color is set. This is the hexidecimal / HTML color value (includes alpha).")]
        public UnityEvent<string> onColorHex;

        private void Start()
        {
            if (setOnStart)
                Set(color);
        }

        /// <summary>
        /// Sets the value using the <see cref="color"/>.
        /// </summary>
        public void Set() => Set(color);

        /// <summary>
        /// Set the color using <see cref="Color32"/> instead of <see cref="Color"/>.
        /// </summary>
        /// <param name="color32">The 32-bit color value.</param>
        public void Set32(Color32 color32) => Set((Color)color32);

        /// <summary>
        /// Set the color value.
        /// </summary>
        /// <param name="color">The color.</param>
        public void Set(Color color)
        {
            this.color = color;
            onColor?.Invoke(color);
            onColor32?.Invoke(color);
            onColorHex?.Invoke(ColorUtility.ToHtmlStringRGBA(color));
        }

        /// <summary>
        /// Set the hex color value.
        /// </summary>
        /// <param name="hexCode">The hexadecimal code.</param>
        public void SetHex(string hexCode)
        {
            hexCode = hexCode.Replace("#", string.Empty);
            if (ColorUtility.TryParseHtmlString(hexCode, out Color color))
                Set(color);
        }
    }
}
