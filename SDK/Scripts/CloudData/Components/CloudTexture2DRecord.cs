using System;
using System.IO;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Rendering;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    /// <summary>
    /// A cloud data record that stores a texture 2D.
    /// </summary>
    public partial class CloudTexture2DRecord : CloudDataRecordBase<Texture2D>
    {
        [Serializable]
        public class TextureEvents
        {
            public UnityEvent<Texture2D> onInputTexture;
            public UnityEvent<Sprite> onInputSprite;
        }

        [Serializable]
        public class PickerEvents
        {
            public UnityEvent onPickStarted;
            public UnityEvent onPickFinished;
            public UnityEvent onPickCancelled;
            public UnityEvent<string> onPickError;
        }
        
        [Header("Texture 2D")]
        [SerializeField] private Texture2D inputTexture;
        public TextureEvents textureEvents = new();

        [Header("Picker Settings")]
        public string filePickerTitle = "Select Image";
        public PickerEvents pickerEvents = new();

        public Texture2D InputTexture
        {
            get => inputTexture;
            set
            {
                inputTexture = value;
                textureEvents.onInputTexture?.Invoke(inputTexture);
                textureEvents.onInputSprite?.Invoke(InputSprite);
            }
        }

        public Sprite InputSprite
        {
            get => Sprite.Create(inputTexture, new Rect(0, 0, inputTexture.width, inputTexture.height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            set => InputTexture = value.texture;
        }

        public override Texture2D ParseData(CloudDataRecordDto record)
        {
            if (record?.Binary == null || record.Binary.Length == 0)
                return null;
            var texture = TextureCache.ReuseTexture2D();
            texture.LoadImage(record.Binary);
            InputTexture = texture;
            return texture;
        }

        public override void WriteData(CloudDataRecordUpsertForm form)
        {
            form.BinaryValue = InputTexture ? InputTexture.EncodeToPNG() : null;
        }

        public void PickImage()
        {
            pickerEvents.onPickStarted?.Invoke();
            
            void OnPicked(string file, Stream stream)
            {
                if (string.IsNullOrEmpty(file) && stream == null)
                {
                    pickerEvents.onPickCancelled?.Invoke();
                    return;
                }

                try
                {
                    byte[] bytes;
                    if (stream == null)
                        bytes = File.ReadAllBytes(file);
                    else
                    {
                        using var mem = new MemoryStream();
                        stream.CopyTo(mem);
                        bytes = mem.ToArray();
                    }

                    var tex = TextureCache.ReuseTexture2D();
                    tex.LoadImage(bytes);
                    InputTexture = tex;
                    pickerEvents.onPickFinished?.Invoke();
                }
                catch (Exception e)
                {
                    pickerEvents.onPickError?.Invoke(e.Message);
                    pickerEvents.onPickCancelled?.Invoke();
                }
            }

            MetaverseCursorAPI.UnlockCursor();

#if !METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED && UNITY_EDITOR
            OnPicked(UnityEditor.EditorUtility.OpenFilePanel(filePickerTitle, "", "|Image Files|*.BMP;*.bmp;*.JPG;*.JPEG*.jpg;*.jpeg;*.PNG;*.png;*.GIF;*.gif;*.tif;*.tiff;"), null);
#else
            PickInternal(OnPicked);
#endif
        }

        partial void PickInternal(Action<string, Stream> onPicked);
    }
}