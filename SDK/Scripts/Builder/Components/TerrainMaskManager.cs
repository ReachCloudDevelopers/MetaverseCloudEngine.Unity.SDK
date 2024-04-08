using Cysharp.Threading.Tasks;
using DataStructures.ViliWonka.KDTrees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Builder.Components
{
    [Experimental]
    [HideMonoScript]
    public class TerrainMaskManager : TriInspectorMonoBehaviour
    {
        private readonly struct KdTreeTerrainTreeInstancePoint : IKDTreePoint
        {
            public readonly TreeInstance TreeInstance;
            public readonly int TreeIndex;

            public KdTreeTerrainTreeInstancePoint(TreeInstance instance, int treeIndex)
            {
                TreeInstance = instance;
                TreeIndex = treeIndex;
                Position = instance.position;
            }

            public Vector3 Position { get; }
            public float GetPositionAxis(int index) => Position[index];
        }

        [SerializeField] private bool applyOnEnable = true;
        [SerializeField, Min(1)] private int treeIndicesToProcessPerFrame = 15;
        [SerializeField, Min(1)] private int pointsToProcessPerFrame = 15;

        private bool _started;
        private bool _isGenerating;
        private bool _dirty;
        private bool _memoryCachingEnabled;

        private readonly Dictionary<Terrain, TerrainData> _originalTerrainDataCache = new();
        private readonly Dictionary<Terrain, TerrainData> _intermediateTerrainDataCache = new();
        private readonly Dictionary<Terrain, KDTree<KdTreeTerrainTreeInstancePoint>> _originalTerrainTreeDataCache = new();
        private readonly List<TerrainMaskManager> _terrainMaskManagers = new();
        private readonly List<Terrain> _terrainsBeingCached = new();

        public static TerrainMaskManager Instance { get; private set; }

        private void Awake()
        {
            _memoryCachingEnabled = Application.platform != RuntimePlatform.WebGLPlayer;
            
            if (!Instance)
                Instance = this;
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            _terrainMaskManagers.Add(this);
        }

        private void Start()
        {
            if (applyOnEnable)
                ApplyMasks();
            _started = true;
        }

        private void OnEnable()
        {
            if (!_started)
                return;
            if (applyOnEnable)
                ApplyMasks();
        }

        private void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting)
                return;

            if (_terrainMaskManagers.Count - 1 == 0)
                ClearTerrainsCache();

            _terrainMaskManagers.Remove(this);
        }

        [Button("Apply Masks")]
        public void ApplyMasks()
        {
            if (!Application.isPlaying) return;
            ApplyMasksAsync(default).Forget();
        }

        public UniTask ApplyMasksAsync(CancellationToken cancellationToken)
        {
            if (_isGenerating)
            {
                _dirty = true;
                return UniTask.CompletedTask;
            }

            _isGenerating = true;

            cancellationToken.Register(() =>
            {
                if (_isGenerating)
                {
                    EndGeneration(cancellationToken);
                }
            });

            return UniTask.Create(async () =>
            {
                try
                {
                    EnsureCache();

                    var tasks = Enumerable.Select(
                        MVUtils.FindObjectsOfTypeNonPrefabPooled<TerrainMask>(true),
                        mask => ApplyMask(mask, cancellationToken)).ToList();

                    await UniTask.WhenAll(tasks);

                    ApplyIntermediateCache();
                }
                finally
                {
                    EndGeneration(cancellationToken);
                }

            }).AttachExternalCancellation(cancellationToken);
        }

        private void EndGeneration(CancellationToken cancellationToken)
        {
            _isGenerating = false;
            if (!_dirty) return;
            _dirty = false;
            if (!cancellationToken.IsCancellationRequested)
                ApplyMasksAsync(cancellationToken).Forget();
        }

        private void ApplyIntermediateCache()
        {
            if (!_memoryCachingEnabled)
            {
                ClearTerrainsCache();
                return;
            }

            foreach (var terrain in _intermediateTerrainDataCache)
                terrain.Key.terrainData = terrain.Value;
            _intermediateTerrainDataCache.Clear();
        }

        private void EnsureCache()
        {
            if (!_memoryCachingEnabled)
                return;

            var terrains = Terrain.activeTerrains;
            foreach (var terrain in terrains)
            {
                if (!IsTerrainCached(terrain))
                    CacheTerrain(terrain);
            }
        }

        private void RefreshIntermediateCache(Terrain terrain)
        {
            if (!_intermediateTerrainDataCache.TryGetValue(terrain, out _) && _originalTerrainDataCache.TryGetValue(terrain, out var data))
                _intermediateTerrainDataCache[terrain] = Instantiate(data);
        }

        private UniTask ApplyMask(TerrainMask mask, CancellationToken cancellationToken)
        {
            cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy(), 
                mask.GetCancellationTokenOnDestroy(), 
                cancellationToken)
                .Token;

            return UniTask.Create(async () =>
            {
                await UniTask.WaitUntil(() => _terrainsBeingCached.Count == 0, cancellationToken: cancellationToken);

                var results = new List<int>();
                foreach (var terrain in Terrain.activeTerrains)
                {
                    var bounds = mask.gameObject.GetWorldCollisionBounds();
                    var terrainTransform = terrain.transform;

                    var terrainData = _memoryCachingEnabled && _intermediateTerrainDataCache.TryGetValue(terrain, out var td) ? td : terrain.terrainData;
                    var terrainBounds = terrainData.bounds;
                    terrainBounds.center += terrainTransform.position;
                    if (!terrain || !terrainBounds.Intersects(bounds))
                        continue;

                    RefreshIntermediateCache(terrain);

                    var tPosition = terrainTransform.position;
                    var maskRelativeBoundsMin = bounds.min.ToTerrainSpace(tPosition, terrainData);
                    var maskRelativeBoundsMax = bounds.max.ToTerrainSpace(tPosition, terrainData);
                    if (mask.removeTrees)
                    {
                        var treePoints = 
                            _memoryCachingEnabled 
                                ? _originalTerrainTreeDataCache.TryGetValue(terrain, out var p) 
                                    ? p : null 
                                : new KDTree<KdTreeTerrainTreeInstancePoint>(await ComputeTerrainTreePoints(terrainData, cancellationToken));

                        var query = new KDQuery();
                        query.Interval(treePoints, maskRelativeBoundsMin, maskRelativeBoundsMax, results);

                        var idx = 0;
                        var pointsLength = treePoints.Points.Length;
                        var treesUpdated = false;
                        for (var index = results.Count - 1; index >= 0; index--)
                        {
                            var treeIndex = results[index];
                            idx++;
                            if (idx > treeIndicesToProcessPerFrame)
                            {
                                idx = 0;
                                await UniTask.Yield(cancellationToken: cancellationToken);
                            }

                            if (treeIndex >= pointsLength)
                                continue;

                            var tree = treePoints.Points[treeIndex];
                            var instance = tree.TreeInstance;
                            instance.widthScale = 0;
                            instance.heightScale = 0;
                            terrainData.SetTreeInstance(tree.TreeIndex, instance);
                            treesUpdated = true;
                        }

                        if (treesUpdated && terrain)
                        {
                            if (terrain.TryGetComponent(out TerrainCollider terrainCollider))
                            {
                                terrainCollider.terrainData = terrainData;
                                terrainCollider.enabled = false;
                                terrainCollider.enabled = true;
                            }
                        }
                    }

                    if (!terrain)
                        continue;

                    if (mask.removeDetails)
                    {
                        TerrainData data;
                        if (_memoryCachingEnabled) _intermediateTerrainDataCache.TryGetValue(terrain, out data);
                        else data = terrain.terrainData;

                        if (data)
                        {
                            var xBase = Mathf.Max(0, (int)(maskRelativeBoundsMin.x * data.detailWidth));
                            var yBase = Mathf.Max(0, (int)(maskRelativeBoundsMin.z * data.detailHeight));
                            var width = Mathf.Max(0, Mathf.Min(data.detailWidth, (int)(maskRelativeBoundsMax.x * data.detailWidth)) - xBase);
                            var height = Mathf.Max(0, Mathf.Min(data.detailHeight, (int)(maskRelativeBoundsMax.z * data.detailHeight)) - yBase);
                            var layers = data.GetSupportedLayers(xBase, yBase, width, height);
                            var blankData = new int[width, height];
                            foreach (var layer in layers)
                                data.SetDetailLayer(xBase, yBase, layer, blankData);
                        }
                    }

                    await UniTask.Yield(cancellationToken: cancellationToken);
                }
            }).AttachExternalCancellation(cancellationToken: cancellationToken);
        }

        private void CacheTerrain(Terrain terrain)
        {
            _originalTerrainDataCache[terrain] = null;
            _terrainsBeingCached.Add(terrain);

            UniTask.Void(async cancelSource =>
            {
                try
                {
                    var terrainData = terrain.terrainData;
                    var terrainTreePoints = await ComputeTerrainTreePoints(terrainData, cancelSource.Token);
                    if (!terrain)
                        return;

                    _originalTerrainTreeDataCache[terrain] = new KDTree<KdTreeTerrainTreeInstancePoint>(terrainTreePoints);
                    _originalTerrainDataCache[terrain] = terrainData;
                    _intermediateTerrainDataCache[terrain] = Instantiate(terrainData);
                    terrain.terrainData = Instantiate(terrainData);
                }
                finally
                {
                    if (terrain)
                        _terrainsBeingCached.Remove(terrain);
                }

            }, CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy(), terrain.GetCancellationTokenOnDestroy()));
        }

        private async Task<KdTreeTerrainTreeInstancePoint[]> ComputeTerrainTreePoints(TerrainData terrainData, CancellationToken cancellationToken = default)
        {
            var terrainTreePoints = new KdTreeTerrainTreeInstancePoint[terrainData.treeInstanceCount];
            var chunks = Mathf.CeilToInt(terrainTreePoints.Length / (float)pointsToProcessPerFrame);

            const int maxChunksToProcess = 2;

            var idx = 0;
            for (var i = 0; i < chunks; i++)
            {
                idx++;
                if (idx > maxChunksToProcess)
                {
                    await UniTask.Yield(cancellationToken: cancellationToken);
                    idx = 0;
                }

                var startIndex = i * pointsToProcessPerFrame;
                var endIndex = Mathf.Min(startIndex + pointsToProcessPerFrame, terrainTreePoints.Length - 1);

                for (var j = startIndex; j <= endIndex; j++)
                    terrainTreePoints[j] = new KdTreeTerrainTreeInstancePoint(terrainData.GetTreeInstance(j), j);
            }

            return terrainTreePoints;
        }

        private void ClearTerrainsCache()
        {
            _originalTerrainDataCache.Clear();
            _originalTerrainTreeDataCache.Clear();
            _intermediateTerrainDataCache.Clear();
        }

        private bool IsTerrainCached(Terrain terrain)
        {
            return _originalTerrainDataCache.TryGetValue(terrain, out _);
        }
    }
}
