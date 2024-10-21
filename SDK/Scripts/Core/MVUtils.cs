using TMPro;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using NetworkObject = MetaverseCloudEngine.Unity.Networking.Components.NetworkObject;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;
using MetaverseCloudEngine.Unity.Web.Implementation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.VisualScripting;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Bounds = UnityEngine.Bounds;
using MethodAttributes = System.Reflection.MethodAttributes;

#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine.XR.ARCore;
#endif

#if !UNITY_IOS
using UnityEngine.XR;
using UnityEngine.XR.Management;

#if !GOOGLE_PLAY
using UnityEngine.XR.OpenXR;

#if MV_OCULUS_PLUGIN || !UNITY_2022_2_OR_NEWER
using Unity.XR.Oculus;
#endif

using UnityEngine.XR.OpenXR.Features;

#if UNITY_2022_2_OR_NEWER
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
#endif

#endif
#endif

namespace MetaverseCloudEngine.Unity
{
    // ReSharper disable once InconsistentNaming
    public static class MVUtils
    {
        #region HTTP

        public static UnityWebRequest WithHttpRequestMessageData(this UnityWebRequest uwr, HttpRequestMessage request, UploadHandler uploadHandler = null, DownloadHandler downloadHandler = null)
        {
            if (downloadHandler != null)
                uwr.downloadHandler = downloadHandler;

            if (uploadHandler != null)
                uwr.uploadHandler = uploadHandler;

            if (request.Content?.Headers is { ContentType: not null } && uwr.uploadHandler != null)
                uwr.uploadHandler.contentType = request.Content.Headers.ContentType.ToString();

            foreach (var (key, value) in request.Headers)
                uwr.SetRequestHeader(key, value.First());

            return uwr;
        }

        public static UnityWebRequest ToUnityWebRequest(this HttpRequestMessage request, UploadHandler uploadHandler = null, DownloadHandler downloadHandler = null)
        {
            var uwr = new UnityWebRequest(request.RequestUri, request.Method.Method);
            return uwr.WithHttpRequestMessageData(request, uploadHandler, downloadHandler);
        }

        public static HttpResponseMessage ToHttpResponseMessage(this UnityWebRequest request)
        {
            var resp = new HttpResponseMessage
            {
                StatusCode = (HttpStatusCode)request.responseCode,
            };

            var respHeaders = request.GetResponseHeaders();
            if (respHeaders is null) 
                return resp;
            
            foreach (var (key, value) in respHeaders)
                resp.Headers.TryAddWithoutValidation(key, value);

            return resp;
        }

        public static IDictionary<string, string> GetQueryParts(this string url)
        {
            var launchUrls = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(url))
                return launchUrls;
            try
            {
                var uri = new Uri(url);
                var values = uri.Query
                    .Replace("?", string.Empty)
                    .Split("&")
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                foreach (var value in values)
                {
                    var keyPair = value.Split("=");
                    if (keyPair.Length == 2)
                        launchUrls[keyPair[0].ToLower()] = keyPair[1];
                }

                return launchUrls;
            }
            catch(Exception e)
            {
                MetaverseProgram.Logger.LogWarning($"Interpreting URL ('{url}') failed: {e}");
                return launchUrls;
            }
        }

        public static void OpenURL(string url, Action onOpened = null, Action onFailed = null)
        {
            if (!url.Contains("://") || url.StartsWith("http://") || url.StartsWith("file://"))
            {
                MetaverseProgram.Logger.LogError("Attempted to open '" + url + "' but it was not in an appropriate format.");
                onFailed?.Invoke();
                return;
            }

#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (MetaverseInternalResources.Instance.blackListedDomains.Any(x => url.StartsWith(x) || url.Replace("www.", string.Empty).StartsWith(x)))
            {
                onFailed?.Invoke();
                return;
            }

            if (url.StartsWith(MetaverseConstants.Urls.WebGL) ||
                MetaverseInternalResources.Instance.whiteListedDomains.Any(x => url.StartsWith(x) || url.Replace("www.", string.Empty).StartsWith(x)))
            {
                Application.OpenURL(url);
                onOpened?.Invoke();
                return;
            }

            if (MetaverseProgram.RuntimeServices?.InternalNotificationManager != null)
                MetaverseProgram.RuntimeServices.InternalNotificationManager.ShowDialog("Opening URL", "You are about to navigate to '" + url + "'. Is that ok?", "Yes", "Cancel",
                    () =>
                    {
                        Application.OpenURL(url);
                        onOpened?.Invoke();
                    },
                    () =>
                    {
                        onFailed?.Invoke();
                    });
            else if (MetaverseProgram.AppUpdateRequired)
            {
                Application.OpenURL(url);
                onOpened?.Invoke();
            }
            return;
#endif

#pragma warning disable CS0162 // Unreachable code detected
            // ReSharper disable once HeuristicUnreachableCode
            MetaverseProgram.Logger.Log($"{url} would have opened. For your safety this functionality is disabled.");
#pragma warning restore CS0162 // Unreachable code detected
        }

        #endregion

        #region Runtime-Safe Editor Functions

        public static void ReplaceScript<T>(this MonoBehaviour behaviour) where T : MonoBehaviour
        {
#if UNITY_EDITOR
            if (behaviour == null)
                return;

            var tempObj = new GameObject("tempOBJ") { hideFlags = HideFlags.HideAndDontSave };
            var inst = tempObj.AddComponent<T>();
            var yourReplacementScript = UnityEditor.MonoScript.FromMonoBehaviour(inst);
            UnityEngine.Object.DestroyImmediate(tempObj);

            UnityEditor.SerializedObject so = new(behaviour);
            var scriptProperty = so.FindProperty("m_Script");
            so.Update();
            scriptProperty.objectReferenceValue = yourReplacementScript;
            so.ApplyModifiedProperties();
#endif
        }

        public static bool IsEditorSimulator()
        {
#if UNITY_EDITOR
            return UnityEngine.Device.SystemInfo.deviceType != DeviceType.Desktop;
#else
            return false;
#endif
        }

        public static bool IsEditorGameView()
        {
#if UNITY_EDITOR
            return UnityEngine.Device.SystemInfo.deviceType == DeviceType.Desktop;
#else
            return false;
#endif
        }

        public static bool IsPrefab(this GameObject go)
        {
#if UNITY_EDITOR
            if (!go) return false;
            try
            {
                if (UnityEditor.EditorUtility.IsPersistent(go))
                    return true;
                if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go))
                    return true;
                return false;
            }
            catch (NullReferenceException)
            {
                return true; // For some reason GetPrefabStage does this...
            }
#else
            return false;
#endif
        }

        public static void SetDirty(this UnityEngine.Object target)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(target);
#endif
        }
        
        [Obsolete("Use SafeDestroy instead.")]
        public static void PlayModeSafeDestroy(this UnityEngine.Object o)
        {
            SafeDestroy(o);
        }

        public static void SafeDestroy(this UnityEngine.Object o)
        {
            if (!o) return;
            
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(o);
            else
                UnityEngine.Object.DestroyImmediate(o);
        }
        
        #endregion

        #region Graphics

        public static Texture2D Copy2D(this Texture source)
        {
            if (Application.isBatchMode)
                return null;
            
            var renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Default);

            Graphics.Blit(source, renderTex);

            var previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            try
            {
                var readableText = new Texture2D(source.width, source.height);
                readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                readableText.Apply();
                return readableText;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTex);
            }
        }

        #endregion

        #region NavMesh

        public static NavMeshHit FindClosestNavMeshEdge(this NavMeshAgent agent)
        {
            agent.FindClosestEdge(out var hit);
            return hit;
        }

        public static NavMeshHit FindClosestNavMeshEdge(this Vector3 position)
        {
            NavMesh.FindClosestEdge(position, out var hit, NavMesh.AllAreas);
            if (Application.isEditor)
            {
                // IL2CPP to prevent stripping
                var pos = hit.position;
                var normal = hit.normal;
                var distance = hit.distance;
                var mask = hit.mask;
                var isHit = hit.hit;
                hit.position = pos;
                hit.normal = normal;
                hit.distance = distance;
                hit.mask = mask;
                hit.hit = isHit;
            }
            return hit;
        }
        
        public static bool IsOnNavMesh(this Vector3 position)
        {
            return position.IsOnNavMesh(0.1f, false, NavMesh.AllAreas);
        }

        public static bool IsOnNavMesh(this Vector3 position, float sampleDistance)
        {
            return position.IsOnNavMesh(sampleDistance, false, NavMesh.AllAreas);
        }
        
        public static bool IsOnNavMesh(this Vector3 position, float sampleDistance, bool sampleY, int areaMask)
        {
            if (sampleY)
                return NavMesh.SamplePosition(position, out _, sampleDistance, areaMask);
            
            var sample = NavMesh.SamplePosition(position, out var hit, Mathf.Infinity, areaMask);
            if (!sample) return false;
            var hit2d = new Vector3(hit.position.x, 0, hit.position.z);
            var pos2d = new Vector3(position.x, 0, position.z);
            return Vector3.Distance(hit2d, pos2d) < sampleDistance;
        }

        #endregion

        #region Behaviour

        public static void ManuallyDeAllocateReferencedAssets(this GameObject o, bool materials = true, bool textures = true, bool meshes = true, bool animationClips = true)
        {
            if (!o)
                return;

            if (meshes)
            {
                var ms = o.GetComponentsInChildren<MeshFilter>(true).Select(x => x.sharedMesh);
                foreach (var mesh in ms)
                {
                    if (!mesh) continue;
                    if (mesh.name is "Cube" or "Sphere" or "Cylinder" or "Capsule" or "Quad" or "Plane")
                        continue;
                    mesh.SafeDestroy();
                }
            }

            if (textures || materials)
            {
                var mats = o.GetComponentsInChildren<Renderer>(true).SelectMany(x => x.sharedMaterials);
                foreach (var material in mats)
                {
                    if (!material) continue;
                    if (textures)
                    {
                        var ts = material.GetTexturePropertyNames().Select(x => material.GetTexture(x));
                        foreach (var texture in ts)
                        {
                            if (!texture) continue;
                            if (!texture.name.StartsWith("Default-") &&
                                texture.name != "Background" &&
                                texture.name != "Checkmark" &&
                                texture.name != "DropdownArrow" &&
                                texture.name != "InputFieldBackground" &&
                                texture.name != "Knob" &&
                                texture.name != "UIMask" &&
                                texture.name != "UISprite")
                                texture.SafeDestroy();
                        }
                    }

                    if (materials && material)
                    {
                        if (!material.name.StartsWith("Default-"))
                            material.SafeDestroy();
                    }
                }
            }

            if (animationClips)
            {
                var animators = o.GetComponentsInChildren<Animator>(true);
                foreach (var animator in animators)
                {
                    if (!animator.runtimeAnimatorController) continue;
                    var clips = animator.runtimeAnimatorController.animationClips;
                    foreach (var clip in clips)
                        clip.SafeDestroy();
                }
            }
        }

        public static RuntimeAnimatorController OverrideAnimations(this RuntimeAnimatorController @base, AnimatorOverrideController layer, bool ignoreNulls = true)
        {
            if (!layer)
                return @base;

            var finalOverrideController = new AnimatorOverrideController(@base);
            var layerOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            layer.GetOverrides(layerOverrides);

            foreach (var ovr in layerOverrides)
            {
                if (!ovr.Value && ignoreNulls)
                    continue;

                try
                {
                    finalOverrideController[ovr.Key.name] = ovr.Value;
                }
                catch { /* ignored */ }
            }

            return finalOverrideController;
        }

        private static Scene _ddolSceneReference;

        public static Scene GetDontDestroyOnLoadScene()
        {
            if (!_ddolSceneReference.IsValid())
                _ddolSceneReference = GetOrCreateDontDestroyOnLoadScene();
            return _ddolSceneReference;
        }

        private static Scene GetOrCreateDontDestroyOnLoadScene()
        {
            if (!Application.isPlaying) return default;
            var ddolSceneHandle = new GameObject(nameof(GetOrCreateDontDestroyOnLoadScene));
            UnityEngine.Object.DontDestroyOnLoad(ddolSceneHandle);
            var scene = ddolSceneHandle.scene;
            UnityEngine.Object.Destroy(ddolSceneHandle);
            return scene;
        }

        public static bool Exists(this UnityEngine.Object obj) => obj;

        public static Transform ResetLocalTransform(this Transform transform, bool position = true, bool rotation = true, bool scale = true)
        {
            if (position)
                transform.localPosition = Vector3.zero;
            if (rotation)
                transform.localRotation = Quaternion.identity;
            if (scale)
                transform.localScale = Vector3.one;
            return transform;
        }

        public static GameObject ResetTransform(this GameObject gameObject)
        {
            return gameObject.transform.ResetLocalTransform().gameObject;
        }

        public static Quaternion InverseTransformRotation(this Transform transform, Quaternion worldRotation)
        {
            return Quaternion.Inverse(transform.rotation) * worldRotation;
        }

        public static Quaternion TransformRotation(this Transform transform, Quaternion localRotation)
        {
            return transform.rotation * localRotation;
        }

        public static Vector3 InverseTransformPointUnscaled(this Transform tr, Vector3 worldPosition)
        {
            return Quaternion.Inverse(tr.rotation) * (worldPosition - tr.position);
        }

        public static Vector3 TransformPointUnscaled(this Transform transform, Vector3 localPosition)
        {
            return transform.position + (transform.rotation * localPosition);
        }

        public static Vector3 ProjectOnPlaneInverse(this Vector3 vector, Vector3 plane)
        {
            return vector - Vector3.ProjectOnPlane(vector, plane);
        }

        public static Vector3 ProjectOnPlane(this Vector3 vector, Vector3 plane)
        {
            return Vector3.ProjectOnPlane(vector, plane);
        }

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.transform.GetOrAddComponent<T>();
        }

        public static T GetOrAddComponent<T, TBase>(this GameObject gameObject, out TBase baseT) 
            where T : Component, TBase
        {
            var t = gameObject.transform.GetOrAddComponent<T>();
            TBase tBase = t;
            baseT = tBase;
            return t;
        }

        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            if (!component.TryGetComponent<T>(out var t))
            {
                t = component.gameObject.AddComponent<T>();
            }

            return t;
        }

        public static T GetNearestComponent<T>(this GameObject gameObject)
        {
            return gameObject.transform.GetNearestComponent<T>();
        }

        public static T GetNearestComponent<T>(this Component component)
        {
            if (component.TryGetComponent(out T cm)) return cm;

            var parent = component.transform.parent;
            if (!parent)
                return GetTopLevelComponentsInChildrenOrdered<T>(component.gameObject).FirstOrDefault();

            while (parent)
            {
                var comps = parent.GetComponentsInChildrenOrdered<T>();
                if (comps.Length > 0)
                    return comps[0];

                parent = parent.parent;
            }

            return default;
        }

        public static T[] GetComponentsInChildrenOrdered<T>(this GameObject gameObject)
        {
            if (!gameObject) return Array.Empty<T>();
            return gameObject.transform.GetComponentsInChildrenOrdered<T>();
        }

        public static T[] GetComponentsInChildrenOrdered<T>(this Component component, List<T> mem = null)
        {
            mem ??= new List<T>();
            mem.AddRange(component.GetComponents<T>());

            for (var i = 0; i < component.transform.childCount; i++)
            {
                var t = component.transform.GetChild(i);
                if (t == component.transform)
                    continue;

                GetComponentsInChildrenOrdered(t, mem);
            }

            return mem.ToArray();
        }

        public static object[] GetComponentsInChildrenOrderedOfType(object obj, Type type)
        {
            if (obj is not GameObject g || !g) return Array.Empty<object>();
            return GetComponentsInChildrenOrderedOfType(g.transform, type);
        }

        public static object[] GetComponentsInChildrenOrderedOfType(this GameObject gameObject, Type type)
        {
            return GetComponentsInChildrenOrderedOfType((object)gameObject, type);
        }

        public static object[] GetComponentsInChildrenOrderedOfType(this Component component, Type type)
        {
            return GetComponentsInChildrenOrderedOfType(component, type, null);
        }

        public static object[] GetComponentsInChildrenOrderedOfType(this Component component, Type type, List<object> mem)
        {
            mem ??= new List<object>();
            mem.AddRange(component.GetComponents(type));

            for (var i = 0; i < component.transform.childCount; i++)
            {
                var t = component.transform.GetChild(i);
                if (t == component.transform)
                    continue;

                GetComponentsInChildrenOrderedOfType(t, type, mem);
            }

            return mem.ToArray();
        }

        public static short GetNetworkObjectBehaviorID<T>(this NetworkObject networkObject, T behavior) where T : MonoBehaviour
        {
            if (!networkObject)
                return -1;
            return (short)Array.IndexOf(networkObject.gameObject.GetTopLevelComponentsInChildrenOrdered<T>(), behavior);
        }

        public static TLookFor[] GetTopLevelComponentsInChildrenOrdered<TLookFor>(this GameObject gameObject)
        {
            return GetTopLevelComponentsInChildrenOrdered<TLookFor, TLookFor>(gameObject);
        }

        public static TLookFor[] GetTopLevelComponentsInChildrenOrdered<TLookFor, TStopAt>(this GameObject gameObject)
        {
            return GetTopLevelComponentsInChildrenOrdered<TLookFor, TStopAt>(gameObject, gameObject);
        }

        private static TLookFor[] GetTopLevelComponentsInChildrenOrdered<TLookFor, TStopAt>(this GameObject gameObject, GameObject root)
        {
            var rootComponents = gameObject.GetComponents<TLookFor>();
            if (typeof(TLookFor).IsAssignableFrom(typeof(TStopAt)))
            {
                var childrenComponents = gameObject.GetComponentsInChildrenOrdered<TLookFor>();
                if (rootComponents.Length > 0)
                    return childrenComponents
                        .Where(tLookFor =>
                        {
                            var tLookForComponent = (Component)(object)tLookFor;
                            if (!tLookForComponent)
                                return false;

                            if (!tLookForComponent.transform.parent)
                                return true;

                            var parentComponent = tLookForComponent.transform.parent.GetComponentInParent<TLookFor>(true);
                            if (parentComponent == null)
                                return false;

                            return rootComponents.Contains(parentComponent);
                        })
                        .ToArray();

                return childrenComponents
                    .Where(tLookFor =>
                    {
                        var tLookForComponent = (Component)(object)tLookFor;
                        if (!tLookForComponent)
                            return false;

                        if (!tLookForComponent.transform.parent)
                            return true;

                        var allParentComponents = tLookForComponent.transform.parent.GetComponentsInParent<TLookFor>(true);
                        if (allParentComponents.Length == 0)
                            return true;

                        allParentComponents = allParentComponents
                            .Where(x =>
                            {
                                var c = (Component)(object)x;
                                return c.gameObject != gameObject && c.transform.IsChildOf(gameObject.transform);
                            })
                            .ToArray();

                        return allParentComponents.Length == 0;
                    })
                    .ToArray();
            }

            var componentsList = new List<TLookFor>(rootComponents.Length);
            componentsList.AddRange(rootComponents);

            if (gameObject != root && gameObject.GetComponent<TStopAt>() != null)
                return componentsList.ToArray();

            for (var i = 0; i < gameObject.transform.childCount; i++)
            {
                var child = gameObject.transform.GetChild(i);
                if (!child) continue;

                var stopAt = child.GetComponents<TStopAt>();
                if (stopAt.Length == 0)
                    componentsList.AddRange(child.gameObject.GetTopLevelComponentsInChildrenOrdered<TLookFor, TStopAt>(root));
            }

            return componentsList.ToArray();
        }

        #endregion

        #region Input

        public static void FlushInputSystem(bool disableAll = true)
        {
            InputSystem.FlushDisconnectedDevices();

            if (disableAll)
            {
                var actions = InputSystem.ListEnabledActions();
                foreach (var action in actions.Where(action => action != null))
                {
                    action.Disable();
                    action.Dispose();
                }

                InputSystem.DisableAllEnabledActions();
            }

            // We need to also call: InputActionState.DisableAllActions(); but the InputActionState type is internal
            // so we'll use reflection to call it.
            var inputActionStateType = Type.GetType("UnityEngine.InputSystem.InputActionState, Unity.InputSystem");
            var disableAllActionsMethod =
                inputActionStateType?.GetMethod("DisableAllActions", BindingFlags.Static | BindingFlags.NonPublic);
            disableAllActionsMethod?.Invoke(null, null);

            // We also want to get the s_GlobalState field and clear all the maps in it.
            var globalStateType =
                Type.GetType("UnityEngine.InputSystem.InputActionState+GlobalState, Unity.InputSystem");
            var globalStateField =
                inputActionStateType?.GetField("s_GlobalState", BindingFlags.Static | BindingFlags.NonPublic);
            var globalState = globalStateField?.GetValue(null);
            var globalListField =
                globalStateType?.GetField("globalList", BindingFlags.Instance | BindingFlags.NonPublic);
            var globalList = globalListField?.GetValue(globalState);
            var lengthField = globalList?.GetType().GetField("length", BindingFlags.Instance | BindingFlags.Public);
            if (lengthField is null) return;

            var length = (int)lengthField.GetValue(globalList);
            for (var i = length - 1; i >= 0; i--)
            {
                var handle = globalList.GetType().GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(globalList, new object[] { i });
                if (handle is null)
                    continue;

                var isAllocated =
                    handle.GetType().GetProperty("IsAllocated", BindingFlags.Instance | BindingFlags.Public)
                        ?.GetValue(handle) as bool?;
                if (isAllocated is false)
                    continue;

                var target = handle.GetType().GetProperty("Target", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(handle);
                target?.GetType().GetMethod("Destroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(target, new object[] { false });
            }

            if (length > 0)
                MetaverseProgram.Logger.Log("[MetaSpaceSceneManager] Released " + length +
                                            " lingering input action states.");
        }

        #endregion

        #region Meta

        public static void SafelyAdjustXRResolutionScale(float scale)
        {
#if !UNITY_IOS && !MV_OCULUS_PLUGIN
            if (!Application.isPlaying)
                return;
            if (Mathf.Abs(scale - XRSettings.eyeTextureResolutionScale) > 0.01f)
                XRSettings.eyeTextureResolutionScale = scale;
#endif
        }

        public static IEnumerable<Guid> GetMetaPrefabSpawnerIds(this GameObject gameObject, bool requireLoadOnStart = true, bool checkCanSpawn = true)
        {
            return !gameObject
                ? Array.Empty<Guid>()
                : GetMetaPrefabSpawners(gameObject, requireLoadOnStart: requireLoadOnStart, checkCanSpawn: checkCanSpawn).Select(x => x.ID!.Value)
                    .Distinct();
        }

        public static IEnumerable<MetaPrefabSpawner> GetMetaPrefabSpawners(this GameObject gameObject, bool requireLoadOnStart = true, bool checkCanSpawn = true)
        {
            return gameObject
                .GetComponentsInChildrenOrdered<MetaPrefabSpawner>()
                .Where(x => x && x.ID.HasValue && (!requireLoadOnStart || x.spawnOnStart) && (!checkCanSpawn || x.HasSpawnAuthority()));
        }

        public static MetaSpace GetMetaSpace(this GameObject gameObject)
        {
            if (gameObject.GetComponent<MetaSpace>())
                return gameObject.GetComponent<MetaSpace>();
            return MetaSpace.Instance ? MetaSpace.Instance : null;
        }

        public static bool HasNetworkObject(this GameObject gameObject)
        {
            return Application.isPlaying
                ? gameObject.GetComponent<NetworkObject>()
                : gameObject.GetComponentsInChildren<NetworkObject>(true)
                    .Any(x => x.gameObject == gameObject);
        }

        public static AssetPlatformDocumentDto GetDocumentForCurrentPlatform(
            this IEnumerable<AssetPlatformDocumentDto> platforms)
        {
            if (platforms is null)
                return null;
            var currentPlatform = MetaverseProgram.GetCurrentPlatform(false);
            var matchingPlatforms = platforms
                .Where(x => x.Platform.HasFlag(currentPlatform))
                .OrderBy(x => x.Document?.UpdatedDate ?? x.Document?.CreatedDate)
                .ToArray();
            var result = matchingPlatforms.FirstOrDefault();
            return result;
        }

        #endregion

        #region Formatting

        public static string CamelCaseToSpaces(this string camelCase)
        {
            var titleCase = Regex.Replace(camelCase, @"(\B[A-Z])", @" $1");
            return titleCase[..1].ToUpper() + titleCase[1..];
        }

        public static string ToYouTubeStyleAgeString(this TimeSpan age, bool pastTense = true)
        {
            return
                age.Days > 365 ? ((age.Days / 365) + " year(s)" + (pastTense ? " ago" : "")) :
                age.Days > 30 ? ((age.Days / 30) + " month(s)" + (pastTense ? " ago" : "")) :
                age.Days > 0 ? (age.Days + " day(s)" + (pastTense ? " ago" : "")) :
                age.Hours > 0 ? (age.Hours + " hour(s)" + (pastTense ? " ago" : "")) :
                age.Minutes > 1 ? (age.Minutes + " minute(s)" + (pastTense ? " ago" : "")) :
                (pastTense ? "Just now" : "Now");
        }

        public static string ToYouTubeStyleFormatString(this int number)
        {
            return ToYouTubeStyleFormatString((long)number);
        }

        public static string ToYouTubeStyleFormatString(this long number)
        {
            return number > 1_000_000 ? Math.Round(number / 1_000_000f, 2).ToString("0.##") + "M" :
                number >= 1_000 ? (number / 1_000f).ToString("0.#") + "K" : number.ToString();
        }

        private static readonly string[] FileSizeLabels = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        public static string ToFileSizeString(this long size, int decimalPlaces = 1)
        {
            if (size <= 0) return "0" + FileSizeLabels[0];
            var bytes = size;
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), decimalPlaces);
            return num.ToString(CultureInfo.InvariantCulture) + FileSizeLabels[place];
        }

        public static string GetFileName(this string path, bool withExtension = false)
        {
            return withExtension ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
        }

        public static string ToPrettyErrorString(this object e, string defaultMessage = "Something went wrong.")
        {
            if (e == null) return defaultMessage;
            var err = e is not Exception ex ? e.ToString() : ex.Message;
            if (string.IsNullOrEmpty(err))
                return defaultMessage;
            if (err.Contains("Exception of type") || err.Contains("Exception from"))
                return defaultMessage;
            return err;
        }

        #endregion

        #region Conversion

        public static Color ToColor(this int argb)
        {
            var sysColor = System.Drawing.Color.FromArgb(argb);
            return new Color(sysColor.R / 255f, sysColor.G / 255f, sysColor.B / 255f, sysColor.A / 255f);
        }

        public static int ToArgb(this Color color)
        {
            var sysColor = System.Drawing.Color.FromArgb((int)(color.a * 255), (int)(color.r * 255), (int)(color.g * 255), (int)(color.b * 255));
            return sysColor.ToArgb();
        }

        #endregion

        #region UI

        private const int IsPointerOverUIPerFrame = 10;
        private static int _isPointerOverUITime;
        private static bool _isPointerOverUICached;

        public static bool IsPointerOverUI()
        {
            if (CachedFrameCount <= _isPointerOverUITime)
                return _isPointerOverUICached;
            
            _isPointerOverUITime = CachedFrameCount + IsPointerOverUIPerFrame;

            if (UnityEngine.Device.Application.isMobilePlatform)
            {
                var touch = Input.touches.FirstOrDefault();
                if (Input.touches.Length > 0 && touch.type != TouchType.Indirect)
                {
                    var touchPos = touch.position;
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = touchPos }, hits);
                    _isPointerOverUICached = hits.Count > 0;
                }
            }
            else
            {
                _isPointerOverUICached = ((Func<bool>)(() => EventSystem.current && EventSystem.current.IsPointerOverGameObject()))();
            }

            return _isPointerOverUICached;
        }

        private const int IsUnityInputFieldFocusedPerFrame = 10;
        private static int _isUnityInputFieldFocusedTime;
        private static bool _isUnityInputFieldFocusedCached;

        public static bool IsUnityInputFieldFocused()
        {
            if (CachedFrameCount <= _isUnityInputFieldFocusedTime) 
                return _isUnityInputFieldFocusedCached;
            
            _isUnityInputFieldFocusedTime = CachedFrameCount + IsUnityInputFieldFocusedPerFrame;
            _isUnityInputFieldFocusedCached = ((Func<bool>)(() =>
            {
                if (!EventSystem.current) return false;
                var selectedObj = EventSystem.current.currentSelectedGameObject;
                if (selectedObj == null) return false;
                var tmpInputField = selectedObj.GetComponent<TMP_InputField>();
                var inputField = selectedObj.GetComponent<InputField>();
                if (tmpInputField != null)
                    return tmpInputField.isFocused;
                return inputField != null && inputField.isFocused;
            }))();

            return _isUnityInputFieldFocusedCached;
        }

        public static Vector2 FindScaledPoint(this Vector2 point, Vector2 sourceImageSize, Vector2 targetImageSize)
        {
            return Vector2.zero;
        }

        #endregion

        #region Reflection

        public static TTo CopyTo<TFrom, TTo>(this TFrom source, TTo destination)
        {
            // If any this null throw an exception
            if (source == null || destination == null)
                throw new Exception("Source or/and Destination Objects are null");
            // Getting the Types of the objects
            var typeDest = destination.GetType();
            var typeSrc = source.GetType();

            // Iterate the Properties of the source instance and  
            // populate them from their destination counterparts  
            var srcProps = typeSrc.GetProperties();
            foreach (var srcProp in srcProps)
            {
                if (!srcProp.CanRead)
                    continue;

                var targetProperty = typeDest.GetProperty(srcProp.Name);
                if (targetProperty == null)
                    continue;
                if (!targetProperty.CanWrite)
                    continue;
                if (targetProperty.GetSetMethod(true) != null && targetProperty.GetSetMethod(true).IsPrivate)
                    continue;
                if ((targetProperty.GetSetMethod().Attributes & MethodAttributes.Static) != 0)
                    continue;
                if (!targetProperty.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                    continue;

                targetProperty.SetValue(destination, srcProp.GetValue(source, null), null);
            }

            // Iterate the Properties of the source instance and  
            // populate them from their destination counterparts  
            var srcFields = typeSrc.GetFields();
            foreach (var srcField in srcFields)
            {
                if (srcField.IsInitOnly || srcField.IsStatic)
                    continue;

                var targetField = typeDest.GetField(srcField.Name);
                if (targetField == null)
                    continue;
                if (targetField.IsInitOnly || targetField.IsStatic)
                    continue;
                if (!targetField.FieldType.IsAssignableFrom(srcField.FieldType))
                    continue;

                targetField.SetValue(destination, srcField.GetValue(source));
            }

            return destination;
        }

        public static IEnumerable<Type> GetAllTypes<T>()
        {
#if UNITY_EDITOR
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypesSafely())
                .Where(x => typeof(T).IsAssignableFrom(x));
#else
            return Enumerable.Empty<Type>();
#endif
        }

        public static T[] CreateClassInstancesOfType<T>()
        {
#if UNITY_EDITOR
            return GetAllTypes<T>()
                .Where(x => x.IsClass && !x.IsAbstract)
                .Select(t => (T)FormatterServices.GetUninitializedObject(t))
                .ToArray();
#else
            return Array.Empty<T>();
#endif
        }
        #endregion

        #region Maths

        public static bool IsLayerInLayerMask(LayerMask layerMask, int layer)
        {
            return layerMask == (layerMask | (1 << layer));
        }

        public static bool IsNaN(this Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
        }

        public static string GenerateUid(int length = 22)
        {
            var uid = Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "");
            return uid.Length >= length ? uid[..length] : uid;
        }

        public static double LerpD(double a, double b, double t)
        {
            return a + (b - a) * Clamp01D(t);
        }

        public static double Clamp01D(double value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        public static Vector3 ToTerrainSpace(this Vector3 worldPos, Vector3 terrainPos, TerrainData terrain)
        {
            worldPos -= terrainPos;
            var xDelta = worldPos.x / terrain.size.x;
            var yDelta = worldPos.y / terrain.size.y;
            var zDelta = worldPos.z / terrain.size.z;
            return new Vector3(xDelta, yDelta, zDelta);
        }

        public static Vector3 AveragePosition(this List<Transform> trs)
        {
            if (trs.Count == 1 && trs[0]) return trs[0].position;
            var result = Vector3.zero;
            for (var i = 0; i < trs.Count; i++)
                if (trs[i]) result += trs[i].transform.position;
            return result / trs.Count;
        }

        public static Vector3 AveragePosition(this Transform[] trs)
        {
            if (trs.Length == 1 && trs[0]) return trs[0].position;
            var result = Vector3.zero;
            for (var i = 0; i < trs.Length; i++)
                if (trs[i]) result += trs[i].transform.position;
            return result / trs.Length;
        }

        public static Vector3 AveragePosition(this List<GameObject> objects)
        {
            if (objects.Count == 1 && objects[0]) return objects[0].transform.position;
            var result = Vector3.zero;
            for (var i = 0; i < objects.Count; i++)
                if (objects[i]) result += objects[i].transform.position;
            return result / objects.Count;
        }

        public static Vector3 AveragePosition(this GameObject[] objects)
        {
            if (objects.Length == 1 && objects[0]) return objects[0].transform.position;
            var result = Vector3.zero;
            for (var i = 0; i < objects.Length; i++)
                if (objects[i]) result += objects[i].transform.position;
            return result / objects.Length;
        }

        public static Vector3 AveragePosition(this List<Vector3> vecs)
        {
            if (vecs.Count == 1) return vecs[0];
            var result = Vector3.zero;
            for (var i = 0; i < vecs.Count; i++)
                result += vecs[i];
            return result / vecs.Count;
        }

        public static Vector3 AveragePosition(this Vector3[] vecs)
        {
            if (vecs.Length == 1) return vecs[0];
            var result = Vector3.zero;
            for (var i = 0; i < vecs.Length; i++)
                result += vecs[i];
            return result / vecs.Length;
        }

        public static Vector3 FlattenDirection(this Vector3 v, Vector3 normal, Vector3 groundNormal)
        {
            v = Vector3.ProjectOnPlane(v, normal).normalized * v.magnitude;
            v = Vector3.Cross(v, -normal);
            v = Vector3.Cross(v, groundNormal);
            return v;
        }

        public static Vector3 FlattenDirection(this Vector3 v, Vector3 normal)
        {
            return v.FlattenDirection(normal, normal);
        }
        
        public static Bounds LocalBoundsToWorld(this GameObject gameObject, Bounds localBounds)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(
                gameObject.transform.TransformPoint(localBounds.min),
                gameObject.transform.TransformPoint(localBounds.max));
            return bounds;
        }

        public static Bounds GetLocalTangibleBounds(this GameObject gameObject)
        {
            if (!gameObject)
                return default;

            var boundaries = gameObject.GetComponentsInChildren<Renderer>(true)
                .Select(x =>
                {
                    var bounds = x.bounds;
                    bounds.SetMinMax(
                        gameObject.transform.InverseTransformPoint(bounds.min),
                        gameObject.transform.InverseTransformPoint(bounds.max));
                    return bounds;
                })
                .Concat(
                    gameObject.GetComponentsInChildren<Collider>(true)
                        .Select(x =>
                        {
                            var bounds = x.bounds;
                            bounds.SetMinMax(
                                gameObject.transform.InverseTransformPoint(bounds.min),
                                gameObject.transform.InverseTransformPoint(bounds.max));
                            return bounds;
                        }))
                .ToArray();

            if (boundaries.Length > 0)
            {
                var bounds = boundaries[0];
                for (var i = 1; i < boundaries.Length; i++)
                    bounds.Encapsulate(boundaries[i]);
                return bounds;
            }

            return new Bounds
            {
                center = Vector3.zero,
                size = Vector3.one * 0.1f
            };
        }

        public static Bounds GetWorldCollisionBounds(this GameObject gameObject)
        {
            var colliders = gameObject.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) return default;
            var bounds = colliders[0].bounds;
            for (var i = colliders.Length - 1; i >= 1; i--)
                bounds.Encapsulate(colliders[i].bounds);
            return bounds;
        }

        public static Bounds GetVisibleBounds(this GameObject gameObject)
        {
            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return default;
            var bounds = renderers[0].bounds;
            for (var i = renderers.Length - 1; i >= 1; i--)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        public static Bounds GetVisibleBounds(this GameObject[] objects)
        {
            if (objects == null || objects.Length == 0) return default;
            var bounds = objects[0].GetVisibleBounds();
            for (var i = objects.Length - 1; i >= 1; i--)
                bounds.Encapsulate(objects[i].GetVisibleBounds());
            return bounds;
        }

        public static void FocusOn(this Bounds bounds, Vector3 forward, float marginPercentage, float fieldOfView, out Vector3 outPosition)
        {
            var maxExtent = bounds.extents.magnitude;
            var minDistance = (maxExtent * marginPercentage) / Mathf.Sin(Mathf.Deg2Rad * fieldOfView / 2f);
            outPosition = bounds.center - forward * minDistance;
        }

        public static bool ContainsCompletely(this Bounds outerBounds, Bounds innerBounds)
        {
            var outerBoundsMax = outerBounds.max;
            var outerBoundsMin = outerBounds.min;
            var innerBoundsMax = innerBounds.max;
            var innerBoundsMin = innerBounds.min;
            return outerBoundsMax.x >= innerBoundsMax.x && outerBoundsMax.y >= innerBoundsMax.y && outerBoundsMax.z >= innerBoundsMax.z
                && outerBoundsMin.x <= innerBoundsMin.x && outerBoundsMin.y <= innerBoundsMin.y && outerBoundsMin.z <= innerBoundsMin.z;
        }

        public static bool Contains(this Terrain terrain, Vector3 pos)
        {
            var tpos = terrain.transform.position;
            var tsize = terrain.terrainData.size;
            if (pos.x > tpos.x && pos.z > tpos.z && pos.x < tpos.x + tsize.x && pos.z < tpos.z + tsize.z) return true;
            else return false;
        }

        #endregion

        #region Performance

        public static bool IsOutOfMemory(long memOffset = 0)
        {
            return false; // FIXME
        }

        public static void FreeUpMemory(Action action, bool gcCollect = true, bool delay = true)
        {
            Resources.UnloadUnusedAssets().completed += _ =>
            {
                if (gcCollect) GC.Collect();
                Collect();
            };
            return;

            void Collect()
            {
                if (delay) MetaverseDispatcher.AtEndOfFrame(() => action?.Invoke());
                else action?.Invoke();
            }
        }

        public static async UniTask FreeUpMemoryAsync(bool gcCollect = true, bool delay = true)
        {
            await Resources.UnloadUnusedAssets();
            if (gcCollect) GC.Collect();
            if (delay) await UniTask.Yield();
        }
        
        public static void DestroyOnCancel(this UnityEngine.Object obj, CancellationToken token)
        {
            token.Register(() => UnityEngine.Object.Destroy(obj));
        }

        #endregion

        #region Compatibility
        
        private static AndroidJavaObject _usbManager;

        private static List<AndroidJavaObject> GetConnectedUsbDevices()
        {
            if (_usbManager is null)
                return new List<AndroidJavaObject>();
            
            var deviceList = _usbManager.Call<AndroidJavaObject>("getDeviceList"); // HashMap<String, UsbDevice>
            var deviceListValues = deviceList.Call<AndroidJavaObject>("values"); // Collection<UsbDevice>
            var deviceListIterator = deviceListValues.Call<AndroidJavaObject>("iterator"); // Iterator<UsbDevice>
            var devices = new List<AndroidJavaObject>();
            while (deviceListIterator.Call<bool>("hasNext"))
            {
                var device = deviceListIterator.Call<AndroidJavaObject>("next");
                devices.Add(device);
            }
            return devices;
        }

        public static void RequestUsbPermissions()
        {
            if (Application.platform != RuntimePlatform.Android)
                return;
            
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _usbManager = unityActivity.Call<AndroidJavaObject>("getSystemService", "usb");
            var intent = new AndroidJavaObject("android.content.Intent", "com.ReachCloud.REACHExplorer.USB_PERMISSION");
            var flagImmutable = new AndroidJavaClass("android.app.PendingIntent").GetStatic<int>("FLAG_IMMUTABLE");
            var permissionIntent = new AndroidJavaObject("android.app.PendingIntent").CallStatic<AndroidJavaObject>("getBroadcast", unityActivity, 0, intent, flagImmutable);

            var connectedUsbDevices = GetConnectedUsbDevices();
            MetaverseProgram.Logger.Log("Found " + connectedUsbDevices.Count + " connected USB devices");
            foreach (var device in connectedUsbDevices)
            {
                var hasPermission = _usbManager.Call<bool>("hasPermission", device);
                if (hasPermission) 
                    continue;

                MetaverseProgram.Logger.Log("Requesting permission for USB device");
                _usbManager.Call("requestPermission", device, permissionIntent);
            }
        }

        public static Vector3 GetLinearVelocity(this Rigidbody rigidbody)
        {
#if UNITY_6000_0_OR_NEWER
            return rigidbody.linearVelocity;
#else
            return rigidbody.velocity;
#endif
        }

        public static void SetLinearVelocity(this Rigidbody rigidbody, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = velocity;
#else
            rigidbody.velocity = velocity;
#endif
        }

        public static float GetLinearDamping(this Rigidbody rigidbody)
        {
#if UNITY_6000_0_OR_NEWER
            return rigidbody.linearDamping;
#else
            return rigidbody.drag;
#endif
        }

        public static void SetLinearDamping(this Rigidbody rigidbody, float damping)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearDamping = damping;
#else
            rigidbody.drag = damping;
#endif
        }

        public static float GetAngularDamping(this Rigidbody rigidbody)
        {
#if UNITY_6000_0_OR_NEWER
            return rigidbody.angularDamping;
#else
            return rigidbody.angularDrag;
#endif
        }

        public static void SetAngularDamping(this Rigidbody rigidbody, float damping)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbody.angularDamping = damping;
#else
            rigidbody.angularDrag = damping;
#endif
        }

        public static bool IsVRCompatible()
        {
#if !GOOGLE_PLAY
#if !UNITY_IOS
            if (!XRSettings.enabled)
                return false;
            if (XRGeneralSettings.Instance && XRGeneralSettings.Instance.AssignedSettings && XRGeneralSettings.Instance.AssignedSettings.activeLoaders != null)
                return XRGeneralSettings.Instance.AssignedSettings.activeLoaders.Any(x => x
                    is OpenXRLoader
#if MV_OCULUS_PLUGIN
                    or OculusLoader
#endif
                );
#endif
#endif
            return false;
        }

        public static bool IsOculusPlatform()
        {
#if !GOOGLE_PLAY
#if !UNITY_IOS
            if (!XRSettings.enabled)
                return false;
            
            if (XRGeneralSettings.Instance && 
                XRGeneralSettings.Instance.AssignedSettings && 
                XRGeneralSettings.Instance.AssignedSettings.activeLoader != null)
            {
#if MV_OCULUS_PLUGIN
                if (XRGeneralSettings.Instance.AssignedSettings.activeLoader is OculusLoader)
                    return true;
#endif
#if UNITY_2022_2_OR_NEWER
                if (XRGeneralSettings.Instance.AssignedSettings.activeLoader is OpenXRLoader)
                {
                    OpenXRFeature feature = OpenXRSettings.ActiveBuildTargetInstance.GetFeature<MetaQuestFeature>();
                    return feature && feature.enabled;
                }
#endif
            }
#endif
#endif
            return false;
        }

        public static bool IsAndroidVR()
        {
            var isAndroid = Application.platform == RuntimePlatform.Android;
#if UNITY_EDITOR
            isAndroid = UnityEditor.EditorUserBuildSettings.activeBuildTarget is UnityEditor.BuildTarget.Android;
#endif
            return isAndroid && IsVRCompatible();
        }

        public static bool IsAppleVR()
        {
            var isApple = Application.platform == RuntimePlatform.IPhonePlayer;
#if UNITY_EDITOR
            isApple = UnityEditor.EditorUserBuildSettings.activeBuildTarget is UnityEditor.BuildTarget.iOS;
#endif
            return isApple && IsVRCompatible();
        }

        public static bool IsMobileVR()
        {
            var isMobile = Application.isMobilePlatform;
#if UNITY_EDITOR
            isMobile = UnityEditor.EditorUserBuildSettings.activeBuildTarget is UnityEditor.BuildTarget.Android or UnityEditor.BuildTarget.iOS;
#endif
            return Application.isMobilePlatform && IsVRCompatible();
        }

        public static bool IsARCompatible()
        {
#if !UNITY_IOS
            if (XRGeneralSettings.Instance && XRGeneralSettings.Instance.AssignedSettings && XRGeneralSettings.Instance.AssignedSettings.activeLoaders != null)
            {
#if UNITY_ANDROID || UNITY_EDITOR
                if (XRGeneralSettings.Instance.AssignedSettings.activeLoaders.Any(x => x is ARCoreLoader))
                    return true;
#endif
            }
#endif
            return false;
        }

        #endregion

        #region Threading
        
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            return await task;
        }

        public static async Task<bool> AwaitSemaphore(SemaphoreSlim semaphore, int? timeout = null, CancellationToken cancellationToken = default)
        {
            // ReSharper disable MethodHasAsyncOverload
            try
            {
                if (!MetaverseDispatcher.UseUniTaskThreading)
                    return await semaphore.WaitAsync(timeout ?? Timeout.Infinite, cancellationToken);
                
                if (timeout.HasValue)
                {
                    var start = DateTime.UtcNow;
                    while (!semaphore.Wait(0, cancellationToken))
                    {
                        if ((DateTime.UtcNow - start).TotalMilliseconds > timeout.Value)
                            return false;
                        try
                        {
                            await UniTask.Yield(cancellationToken);
                        }
                        catch (Exception e)
                        {
                            MetaverseProgram.Logger.Log(e);
                            return false;
                        }
                    }
                }
                else
                {
                    while (!semaphore.Wait(0, cancellationToken))
                        await UniTask.Yield(cancellationToken);
                }

                return !cancellationToken.IsCancellationRequested;
            }
            catch
            {
                /* ignored */
                return false;
            }
            // ReSharper restore MethodHasAsyncOverload
        }

        #endregion

        #region Application

        /// <summary>
        /// Captures the command line arguments from Environment.GetCommandLineArgs() and returns them as a dictionary.
        /// </summary>
        /// <returns>The application's command line arguments as a dictionary.</returns>
        public static Dictionary<string, string> GetCommandLineArgs()
        {
            var commandLineArgs = new Dictionary<string, string>();
            var args = Environment.GetCommandLineArgs();
            var key = string.Empty;
            var value = new List<string>();

            foreach (var arg in args)
            {
                var trimmed = arg.Trim();
                if (!trimmed.StartsWith('-'))
                {
                    value.Add(trimmed);
                    continue;
                }

                if (value.Count > 0)
                {
                    if (!string.IsNullOrEmpty(key))
                        commandLineArgs[key] = string.Join(" ", value);
                    value.Clear();
                }

                key = trimmed.TrimStart('-');
            }
            
            if (value.Count > 0 && !string.IsNullOrEmpty(key))
                commandLineArgs[key] = string.Join(" ", value);
            
            return commandLineArgs;
        }

        #endregion

        #region Other

        public static float CachedTime => CachedValues.GetOrCreate().time;
        public static int CachedFrameCount => CachedValues.GetOrCreate().frameCount;

        public static T[] FindObjectsOfTypeNonPrefabPooled<T>(bool includeInactive = false) where T : Component
        {
            return UnityEngine.Object.FindObjectsOfType<T>(includeInactive)
                .Where(x => !x.GetComponentInParent<MetaPrefabPoolContainer>(true))
                .ToArray();

        }

        public static bool IfNull<T>(this T o)
        {
            return o == null;
        }

        public static bool IfNotNull<T>(this T o, Action<T> f = null)
        {
            if (o != null)
            {
                f?.Invoke(o);
                return true;
            }

            return false;
        }

        public static bool If(this bool o, bool match, Action f)
        {
            if (o == match)
                f?.Invoke();
            return o == match;
        }

        public static T Do<T>(this T o, Action<T> f)
        {
            f?.Invoke(o);
            return o;
        }

        public delegate void DoRefAction<T>(ref T item);

        public static T DoRef<T>(this T o, params DoRefAction<T>[] fs)
        {
            if (fs == null)
                return o;
            var copy = o;
            foreach (var f in fs) f?.Invoke(ref copy);
            return copy;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (dict.TryGetValue(key, out var value)) return value;
            value = valueFactory(key);
            dict.Add(key, value);
            return value;
        }

        public static Dictionary<HumanBodyBones, Transform> GetBones(this Animator animator, HumanBodyBones[] ignoredBones = null, HumanBodyBones[] specificBones = null)
        {
            var ignoredBonesList = ignoredBones != null ? ignoredBones.ToList() : new List<HumanBodyBones>();
            var specificBonesList = specificBones != null ? specificBones.ToList() : new List<HumanBodyBones>();
            var dict = new Dictionary<HumanBodyBones, Transform>();
            foreach (var boneType in ((HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones))).Where(x => x >= 0 && x < HumanBodyBones.LastBone))
                if (animator.TryGet(out Transform bone, x => x.GetBoneTransform(boneType), y => y != null) && !ignoredBonesList.Contains(boneType) && (specificBonesList.Count == 0 || specificBonesList.Contains(boneType)))
                    dict[boneType] = bone;
            return dict;
        }

        public static T If<T>(this T o, Func<T, bool> f, Action<T> a)
        {
            if (f(o)) a(o);
            return o;
        }

        public static bool TryGet<TIn, TOut>(this TIn o, out TOut v, Func<TIn, TOut> get, Func<TOut, bool> check)
        {
            v = get(o);
            return check(v);
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> e, Action<T> f)
        {
            var forEach = e as T[] ?? e.ToArray();
            foreach (var o in forEach)
                f?.Invoke(o);
            return forEach;
        }

        public static IEnumerable<T> For<T>(this IEnumerable<T> e, Action<T, int> f)
        {
            var index = 0;
            var enumerable = e as T[] ?? e.ToArray();
            foreach (var o in enumerable)
            {
                index++;
                f?.Invoke(o, index);
            }
            return enumerable;
        }

        // ReSharper disable once InconsistentNaming
        public static T uNull<T>(this T o) where T : UnityEngine.Object
        {
            return o ? o : null;
        }

        public static UniTask<IEnumerable<T>> ForEachPerFrame<T>(this IEnumerable<T> e, int perFrame, int frameDelay, Action<T> f, CancellationToken cancellationToken = default)
        {
            return UniTask.Create(async () =>
            {
                var frame = 0;
                var enumerable = e as T[] ?? e.ToArray();
                foreach (var o in enumerable)
                {
                    frame++;
                    if (frame > perFrame)
                    {
                        frame = 0;
                        await UniTask.DelayFrame(frameDelay, cancellationToken: cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    f?.Invoke(o);
                }

                return (IEnumerable<T>)enumerable;
            });
        }

        public static TOut Return<TIn, TOut>(this TIn o, Func<TIn, TOut> f)
        {
            return f(o);
        }

        public static Dictionary<TKey, TVal> Map<TKey, TVal>(this IEnumerable<TKey> keys, IEnumerable<TVal> values, Func<TKey, TVal, bool> selector)
        {
            var dict = new Dictionary<TKey, TVal>();
            var valuesEnumerable = values as TVal[] ?? values.ToArray();
            foreach (var key in keys)
            {
                foreach (var val in valuesEnumerable)
                {
                    if (!selector(key, val)) 
                        continue;
                    dict[key] = val;
                    break;
                }
            }

            return dict;
        }

        /// <summary>
        /// Sets the layer of this GameObject and all of its descendants
        /// </summary>
        /// <param name="gameObject">The GameObject at the root of the hierarchy that will be modified</param>
        /// <param name="layer">The layer to recursively assign GameObjects to</param>
        public static void SetLayerRecursively(this GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                child.gameObject.SetLayerRecursively(layer);
            }
        }

        public static bool IsUnityNull(this object obj)
        {
            // Checks whether an object is null or Unity pseudo-null
            // without having to cast to UnityEngine.Object manually

            return obj == null || ((obj is UnityEngine.Object o) && !o);
        }

        #endregion

        #region Classes

        // FIXME: Is this even necessary???
        [AddComponentMenu("")]
        private class CachedValues : MonoBehaviour
        {
            public float time;
            public int frameCount;

            private static CachedValues _current;
            private static bool _created;

            public static CachedValues GetOrCreate()
            {
                if (_created) 
                    return _current;
                
                if (!Application.isPlaying)
                    return null;
                _created = true;
                _current = new GameObject(nameof(CachedValues)).AddComponent<CachedValues>();
                var currentGameObject = _current.gameObject;
                currentGameObject.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(currentGameObject);

                return _current;
            }

            public void Update()
            {
                time = Time.time;
                frameCount = Time.frameCount;
            }
        }

        #endregion
    }
}