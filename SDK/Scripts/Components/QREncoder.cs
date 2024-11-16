using UnityEngine;
using UnityEngine.Events;
using ZXing;
using ZXing.QrCode;

namespace MetaverseCloudEngine.Unity.Components
{
    public class QREncoder : MonoBehaviour
    {
        [SerializeField] private string text;
        [SerializeField] private bool generateOnStart = true;

        [Header("Events")]
        public UnityEvent<Texture2D> onGeneratedTexture;
        public UnityEvent<Sprite> onGeneratedSprite;

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

        public void Generate(string text)
        {
            Text = text;
            Generate();
        }

        public void Generate()
        {
            if (onGeneratedTexture.GetPersistentEventCount() == 0 &&
                onGeneratedSprite.GetPersistentEventCount() == 0)
                return;

            Texture2D qr = GenerateQR(text);
            if (onGeneratedTexture.GetPersistentEventCount() > 0)
                onGeneratedTexture?.Invoke(qr);
            if (onGeneratedSprite.GetPersistentEventCount() > 0)
                onGeneratedSprite?.Invoke(Sprite.Create(qr, new Rect(0, 0, qr.width, qr.height), Vector2.zero));
        }

        public Texture2D GenerateQR(string text)
        {
            Texture2D encoded = new Texture2D(256, 256);
            Color32[] color = Encode(text, encoded.width, encoded.height);
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
    }
}
