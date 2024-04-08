using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using TMPro;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.SceneManagement.Components
{
    /// <summary>
    /// Provides a simple API for interacting with the <see cref="MetaSpace"/> component from the Unity inspector.
    /// </summary>
    [DeclareFoldoutGroup("Prefabs")]
    [DeclareFoldoutGroup("Initialization")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Metaverse Assets/Spaces/Meta Space API")]
    [HideMonoScript]
    public partial class MetaSpaceAPI : MetaverseBehaviour
    {
        [Group("Prefabs")] public UnityEvent onLoadingPrefabsStarted;
        [Group("Prefabs")] public UnityEvent onLoadingPrefabsCompleted;

        [Group("Initialization")] public UnityEvent onMetaSpaceInitialized;
        [Group("Initialization")] public UnityEvent onMetaSpaceNotInitialized;

        protected override void Awake()
        {
            base.Awake();

            if (MetaSpace.Instance != null)
            {
                if (!MetaSpace.Instance.LoadedPrefabs)
                {
                    if (MetaSpace.Instance.LoadingPrefabs)
                        onLoadingPrefabsStarted?.Invoke();
                }
                else
                {
                    onLoadingPrefabsCompleted?.Invoke();
                }

                MetaSpace.Instance.LoadingPrefabsStarted += OnLoadingPrefabsStarted;
                MetaSpace.Instance.LoadingPrefabsCompleted += OnLoadingPrefabsCompleted;

                if (MetaSpace.Instance.IsInitialized)
                    onMetaSpaceInitialized?.Invoke();
                else
                {
                    onMetaSpaceNotInitialized?.Invoke();
                    MetaSpace.OnReady(() =>
                    {
                        onMetaSpaceInitialized?.Invoke();
                    });
                }
            }

            AwakeInternal();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            OnDestroyInternal();

            if (MetaSpace.Instance)
            {
                MetaSpace.Instance.LoadingPrefabsStarted -= OnLoadingPrefabsStarted;
                MetaSpace.Instance.LoadingPrefabsCompleted -= OnLoadingPrefabsCompleted;
            }
        }

        partial void AwakeInternal();

        partial void OnDestroyInternal();

        private void OnLoadingPrefabsCompleted() => onLoadingPrefabsCompleted?.Invoke();

        private void OnLoadingPrefabsStarted() => onLoadingPrefabsStarted?.Invoke();

        public void EnterHomeScreen()
        {
            if (MetaSpace.Instance)
            {
                IMetaSpaceNetworkingService netService = MetaSpace.Instance.GetService<IMetaSpaceNetworkingService>();
                if (netService != null && netService.IsOfflineMode)
                {
                    if (Application.isEditor)
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#endif
                    }
                    else
                    {
                        Application.Quit();
                    }

                    return;
                }
            }

            MetaverseProgram.OnInitialized(() => EnterHomeScreenInternal()); // Don't collapse this delegate (because of il2cpp bug)
        }

        partial void EnterHomeScreenInternal();

        public void LoadDeepLink(TMP_Text text) => LoadDeepLink(text.text);

        public void LoadDeepLink(TMP_InputField inputField) => LoadDeepLink(inputField.text);

        public void LoadDeepLink(string url) => LoadDeepLinkInternal(url);

        partial void LoadDeepLinkInternal(string url);

        public void ShareDeepLinkUrl() => ShareDeepLinkUrlInternal();

        partial void ShareDeepLinkUrlInternal();

        public void RejoinInstance()
        {
            RejoinInstanceInternal();
        }

        partial void RejoinInstanceInternal();
    }
}