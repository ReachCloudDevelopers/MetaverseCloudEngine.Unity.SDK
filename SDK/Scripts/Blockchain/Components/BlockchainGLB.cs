using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Blockchain.Components
{
    [Experimental]
    [HideMonoScript]
    public class BlockchainGlb : TriInspectorMonoBehaviour
    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
        private static readonly BlockchainObjectDownloader Downloader = new();
#endif

        [SerializeField] private BlockchainType type;
        [SerializeField] private string assetID;
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private Transform parent;

        [Header("Events")]
        [SerializeField] private UnityEvent onLoadStarted;
        [SerializeField] private UnityEvent onLoadFinished;
        [SerializeField] private UnityEvent<GameObject> onLoadSuccess;
        [SerializeField] private UnityEvent onLoadFailed;

        private CancellationToken _cancellationToken;
        private bool _isLoading;

        public bool LoadOnStart {
            get => loadOnStart;
            set => loadOnStart = value;
        }

        public BlockchainType Type {
            get => type;
            set => type = value;
        }

        public int TypeInt {
            get => (int)type;
            set => type = (BlockchainType)value;
        }

        public string AssetID {
            get => assetID;
            set => assetID = value;
        }

        private void Awake()
        {
            _cancellationToken = this.GetCancellationTokenOnDestroy();
        }

        private void Start()
        {
            if (loadOnStart)
                Load();
        }

        private void Reset()
        {
            parent = transform;
        }

        public void Load()
        {
            if (_isLoading)
                return;

            _isLoading = true;

            MetaverseProgram.OnInitialized(() =>
            {
                onLoadStarted?.Invoke();
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                Downloader.Download($"blockchain://{type}__{assetID}", go =>
                {
                    if (!this)
                        return;

                    if (parent)
                    {
                        go.transform.SetParent(parent);
                        go.transform.ResetLocalTransform();
                    }

                    onLoadSuccess?.Invoke(go);
                    onLoadFinished?.Invoke();
                    _isLoading = false;
                },
                _ =>
                {
                    if (!this)
                        return;

                    onLoadFailed?.Invoke();
                    onLoadFinished?.Invoke();
                    _isLoading = false;
                },
                cancellationToken: _cancellationToken);
                return;
#endif
#pragma warning disable CS0162 // Unreachable code detected
                SimulateDownloadInSDK();
#pragma warning restore CS0162 // Unreachable code detected
            });
        }

        private void SimulateDownloadInSDK()
        {
            MetaverseDispatcher.WaitForSeconds(1f, () =>
            {
                if (!this) return;
                onLoadFailed?.Invoke();
                onLoadFinished?.Invoke();
            });
        }
    }
}
