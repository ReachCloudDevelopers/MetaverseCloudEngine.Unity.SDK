using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using ZXing;
using ZXing.QrCode;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class QREncoder : TriInspectorMonoBehaviour
    {
        [SerializeField] private string text;
        [SerializeField] private bool generateOnStart = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<Texture2D> onGeneratedTexture;
        [SerializeField] private UnityEvent<Sprite> onGeneratedSprite;
        
        public UnityEvent<Texture2D> OnGeneratedTexture => onGeneratedTexture;
        public UnityEvent<Sprite> OnGeneratedSprite => onGeneratedSprite;
        
        private Texture2D _encoded;
        private Sprite _sprite;

        public string Text
        {
            get => text;
            set => text = value;
        }

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// Generates a QR code from the text.
        /// </summary>
        /// <param name="t"></param>
        public void Generate(string t)
        {
            Text = t;
            Generate();
        }

        /// <summary>
        /// Generates a QR code from the <see cref="Text"/>.
        /// </summary>
        public void Generate()
        {
            if (onGeneratedTexture.GetPersistentEventCount() == 0 &&
                onGeneratedSprite.GetPersistentEventCount() == 0)
                return;
            Dispose();
            _encoded = GenerateQr(text);
            if (onGeneratedTexture.GetPersistentEventCount() > 0)
                onGeneratedTexture?.Invoke(_encoded);
            if (onGeneratedSprite.GetPersistentEventCount() > 0)
                onGeneratedSprite?.Invoke(_sprite = Sprite.Create(_encoded, new Rect(0, 0, _encoded.width, _encoded.height), Vector2.zero));
        }

        /// <summary>
        /// Generates a QR code from the text and returns a <see cref="Texture2D"/>.
        /// </summary>
        /// <param name="t">The text to encode.</param>
        /// <returns>The generated QR code as a <see cref="Texture2D"/>.</returns>
        public static Texture2D GenerateQr(string t)
        {
            var encoded = new Texture2D(256, 256);
            var color = Encode(t, encoded.width, encoded.height);
            encoded.SetPixels32(color);
            encoded.Apply();
            return encoded;
        }

        private static Color32[] Encode(string textForEncoding, int width, int height)
        {
            var renderer = new Color32Renderer();
            var writer = new BarcodeWriterGeneric<Color32[]>
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width
                },
                Renderer = renderer,
            };
            return writer.Write(textForEncoding);
        }
        
        private void Dispose()
        {
            if (_encoded)
                Destroy(_encoded);
            if (_sprite)
                Destroy(_sprite);
        }
    }
}
