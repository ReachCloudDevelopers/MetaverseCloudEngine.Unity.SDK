#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Rendering.Components;
using TriInspectorMVCE;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.UI
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class EditorCompatibilityError : TriInspectorMonoBehaviour
    {
        private static readonly Dictionary<GameObject, string> ErrorMessageMap = new();

        /// <summary>
        /// Add an error message overlay to the specified gameobject.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="errorMessage"></param>
        /// <param name="destroyToken"></param>
        public static void For(GameObject gameObject, string errorMessage, CancellationToken destroyToken)
        {
            if (!Application.isPlaying)
                return;
            if (!ErrorMessageMap.TryAdd(gameObject, errorMessage))
                return;
            var followObject = new GameObject("EditorCompatibilityError")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var billboard = followObject.AddComponent<Billboard>();
            billboard.facePositionInVr = true;
            var sprite = followObject.AddComponent<SpriteRenderer>();
            var tex = EditorGUIUtility.IconContent("d_Invalid@2x").image;
            var t2d = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            Graphics.CopyTexture(tex, t2d);
            sprite.sprite = Sprite.Create(t2d, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            sprite.material = new Material(Shader.Find("Sprites/Always On Top"));
            var updateCallbacks = followObject.AddComponent<UnityUpdateEventCallbacks>();
            updateCallbacks.events.onLateUpdate.AddListener(() =>
            {
                followObject.transform.position = gameObject.transform.position;
            });
            destroyToken.Register(() =>
            {
                DestroyImmediate(followObject, true);
                DestroyImmediate(tex, true);
                ErrorMessageMap.Remove(gameObject);
            });
        }
    }
}
#endif