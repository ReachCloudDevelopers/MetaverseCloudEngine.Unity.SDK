using UnityEngine;
using UnityEngine.Events;
using ZXing;
using ZXing.QrCode;

namespace MetaverseCloudEngine.Unity.Components
{
    public class QREncoder : MonoBehaviour
    {
        public string text;
        public bool generateOnStart = true;

        [Header("Events")]
        public UnityEvent<Texture2D> onGeneratedTexture;
        public UnityEvent<Sprite> onGeneratedSprite;

        private void Start()
        {
            if (generateOnStart)
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
            Color[] color = Encode(text, encoded.width, encoded.height);
            encoded.SetPixels(color);
            encoded.Apply();
            return encoded;
        }

        private static Color[] Encode(string textForEncoding, int width, int height)
        {
            BarcodeWriterGeneric<Color[]> writer = new BarcodeWriterGeneric<Color[]>
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width
                }
            };
            return writer.Write(textForEncoding);
        }
    }
}
