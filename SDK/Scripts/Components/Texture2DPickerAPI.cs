using System;
using System.IO;
using MetaverseCloudEngine.Unity.Rendering;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This class allows you to pick an image file from your devices hard-drive,
    /// and provides events for you to handle the result.
    /// </summary>
    public partial class Texture2DPickerAPI : MonoBehaviour
    {
        [Serializable]
        public class Texture2DEvents
        {
            public UnityEvent<Texture2D> onTexture2D;
            public UnityEvent<Sprite> onSprite;
        }

        [Serializable]
        public class PickerEvents
        {
            public UnityEvent onPickStarted;
            public UnityEvent onPickCancelled;
            public UnityEvent onPickFinished;
            public UnityEvent<string> onPickError;
        }

        [Tooltip("The title of the file picker dialog.")]
        public string filePickerTitle = "Select an Image";
        [Tooltip("If true, if this object is destroyed, or a new texture is picked, it will dispose of the old texture reference in memory. Otherwise, it will not.")]
        public bool trackTextureReference = true;
        public PickerEvents pickerEvents = new();
        public Texture2DEvents textureEvents = new();

        private Texture2D _textureReference;
        private Sprite _spriteReference;

        private void OnDestroy() => CleanupReferences();

        private void CleanupReferences()
        {
            if (!trackTextureReference)
                return;

            if (_textureReference)
            {
                Destroy(_textureReference);
                _textureReference = null;
            }

            if (_spriteReference)
            {
                Destroy(_spriteReference);
                _spriteReference = null;
            }
        }

        /// <summary>
        /// This method will open the file picker, and will call the appropriate events when 
        /// picking is finished, cancelled, or an error occurs.
        /// </summary>
        public void PickImage()
        {
            CleanupReferences();
            
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
                        using MemoryStream mem = new MemoryStream();
                        stream.CopyTo(mem);
                        bytes = mem.ToArray();
                    }

                    if (textureEvents.onTexture2D.GetPersistentEventCount() > 0 ||
                        textureEvents.onSprite.GetPersistentEventCount() > 0)
                    {
                        var tex = TextureCache.ReuseTexture2D();
                        tex.LoadImage(bytes);
                        textureEvents.onTexture2D?.Invoke(tex);
                        textureEvents.onSprite?.Invoke(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect));
                    }

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
