#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using System.Threading;
using UnityEngine;
using System;
using System.Linq;

namespace MetaverseCloudEngine.Unity.Components
{
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    public class MetaPrefabEditorPreview : MonoBehaviour
    {
#if UNITY_EDITOR
        private bool _isRequestingPrefab;

        private Guid _lastPrefabId;

        private CancellationTokenSource cancellationTokenSource;
        private MetaPrefabSpawner _spawner;
        private MetaPrefabSpawner Spawner {
            get {
                if (_spawner == null)
                    _spawner = GetComponentInParent<MetaPrefabSpawner>(true);
                return _spawner;
            }
        }

        private GameObject _spawnedPrefab;
        private GameObject SpawnedPrefab {
            get {
                if (!this) return null;
                if (!_spawnedPrefab && transform.childCount > 0)
                    _spawnedPrefab = transform.GetChild(0).gameObject;
                return _spawnedPrefab;
            }
        }

        private void OnValidate()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
        }

        private void OnDestroy()
        {
            DestroyPreview();
        }

        private void Update()
        {
            if (!MetaverseProgram.Initialized)
                return;

            if (!Spawner ||
                !Spawner.previewInEditor ||
                Spawner.ID == null ||
                gameObject.IsPrefab() ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                BuildPipeline.isBuildingPlayer)
            {
                DestroyPreview();
                return;
            }

            transform.ResetLocalTransform();

            if (SpawnedPrefab == null || Spawner.ID != _lastPrefabId)
            {
                SpawnPreview();
            }
        }

        private void DestroyPreview()
        {
            if (SpawnedPrefab) SpawnedPrefab.SafeDestroy();
            _isRequestingPrefab = false;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;
        }

        private void SpawnPreview()
        {
            if (_isRequestingPrefab)
                return;

            DestroyPreview();
            cancellationTokenSource = new CancellationTokenSource();
            _isRequestingPrefab = true;
            _lastPrefabId = Spawner.ID.GetValueOrDefault();
            MetaPrefabLoadingAPI.LoadPrefab(_lastPrefabId, gameObject.scene,
                go =>
                {
                    _spawnedPrefab = Instantiate(go, transform);
                    _spawnedPrefab.transform.ResetLocalTransform();
                    _spawnedPrefab.tag = "EditorOnly";
                    var children = _spawnedPrefab.GetComponentsInChildren<Transform>(true).Where(x => x.transform != transform);
                    foreach (var child in children)
                        child.gameObject.hideFlags = Application.isPlaying ? (HideFlags.HideInHierarchy | HideFlags.NotEditable) : HideFlags.HideAndDontSave;
                    _spawnedPrefab.hideFlags = Application.isPlaying ? (HideFlags.HideInHierarchy | HideFlags.NotEditable) : HideFlags.HideAndDontSave;
                    _isRequestingPrefab = false;
                },
                failed: e =>
                {
                    DestroyPreview();
                    _isRequestingPrefab = false;
                },
                cancellationToken: cancellationTokenSource.Token);
        }
#endif
    }
}
