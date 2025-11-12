using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Common.Enumerations;

namespace MetaverseCloudEngine.Unity.Editors
{
    /// <summary>
    /// Unity Editor window for batch building and uploading MetaSpaces and MetaPrefabs.
    /// </summary>
    public class BatchBuilderWindow : EditorWindow
    {
        #region Nested Types

        [Serializable]
        private class AssetItem
        {
            public string assetPath;
            public string assetName;
            public AssetType assetType;
            public bool isSelected;
            public BuildStatus status = BuildStatus.Pending;
            public string errorMessage;

            public enum AssetType
            {
                MetaSpace,
                MetaPrefab
            }

            public enum BuildStatus
            {
                Pending,
                Building,
                Success,
                Failed
            }
        }

        [Serializable]
        private class BatchConfiguration
        {
            public string batchName = "New Batch";
            public List<string> selectedAssetPaths = new List<string>();
            public bool stopOnFailure = false;
        }

        #endregion

        #region Fields

        private const string EditorPrefsPrefix = "MetaverseBatchBuilder_";
        private const string CurrentBatchKey = EditorPrefsPrefix + "CurrentBatch";
        private const string BatchListKey = EditorPrefsPrefix + "BatchList";
        private const string SearchFilterKey = EditorPrefsPrefix + "SearchFilter";
        private const string AssetTypeFilterKey = EditorPrefsPrefix + "AssetTypeFilter";

        private List<AssetItem> _allAssets = new List<AssetItem>();
        private List<string> _batchNames = new List<string>();
        private Dictionary<string, BatchConfiguration> _batches = new Dictionary<string, BatchConfiguration>();
        private string _currentBatchName;
        private BatchConfiguration _currentBatch;

        private Vector2 _scrollPosition;
        private bool _isBuilding;
        private int _currentBuildIndex;
        private float _buildProgress;
        private string _currentBuildAssetName;

        private string _searchFilter = "";
        private AssetTypeFilter _assetTypeFilter = AssetTypeFilter.All;

        private GUIStyle _statusIconStyle;
        private Texture2D _pendingIcon;
        private Texture2D _buildingIcon;
        private Texture2D _successIcon;
        private Texture2D _failedIcon;
        private bool _stylesInitialized;

        #endregion

        #region Enums

        private enum AssetTypeFilter
        {
            All,
            MetaSpacesOnly,
            MetaPrefabsOnly
        }

        #endregion

        #region Unity Menu

        [MenuItem(MetaverseConstants.MenuItems.WindowsMenuRootPath + "Batch Builder")]
        public static void Open()
        {
            var window = GetWindow<BatchBuilderWindow>();
            window.titleContent = new GUIContent("Batch Builder", MetaverseEditorUtils.EditorIcon);
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            LoadBatches();
            LoadFilters();
            DiscoverAssets();
        }

        private void OnDisable()
        {
            SaveBatches();
            SaveFilters();
        }

        private void OnGUI()
        {
            // Initialize styles on first GUI call (can't be done in OnEnable)
            if (!_stylesInitialized)
            {
                InitializeStyles();
                _stylesInitialized = true;
            }

            MetaverseEditorUtils.Header("Batch Builder");

            EditorGUILayout.Space(5);

            DrawBatchSelector();
            EditorGUILayout.Space(5);

            DrawAssetList();
            EditorGUILayout.Space(5);

            DrawSettings();
            EditorGUILayout.Space(5);

            DrawBuildControls();
        }

        private void Update()
        {
            if (_isBuilding)
            {
                Repaint();
            }
        }

        #endregion

        #region GUI Drawing

        private void DrawBatchSelector()
        {
            MetaverseEditorUtils.Box(() =>
            {
                EditorGUILayout.LabelField("Batch Management", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();

                // Batch dropdown
                var batchIndex = _batchNames.IndexOf(_currentBatchName);
                if (batchIndex < 0) batchIndex = 0;

                var newBatchIndex = EditorGUILayout.Popup("Current Batch", batchIndex, _batchNames.ToArray());
                if (newBatchIndex != batchIndex && newBatchIndex >= 0 && newBatchIndex < _batchNames.Count)
                {
                    SwitchToBatch(_batchNames[newBatchIndex]);
                }

                // New Batch button
                if (GUILayout.Button("New Batch", GUILayout.Width(100)))
                {
                    CreateNewBatch();
                }

                // Delete Batch button
                MetaverseEditorUtils.Disabled(() =>
                {
                    if (GUILayout.Button("Delete Batch", GUILayout.Width(100)))
                    {
                        DeleteCurrentBatch();
                    }
                }, _batchNames.Count <= 1);

                EditorGUILayout.EndHorizontal();

                // Rename batch
                if (_currentBatch != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Batch Name", GUILayout.Width(EditorGUIUtility.labelWidth));
                    var newName = EditorGUILayout.TextField(_currentBatch.batchName);
                    if (newName != _currentBatch.batchName && !string.IsNullOrWhiteSpace(newName))
                    {
                        RenameBatch(newName);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            });
        }

        private void DrawAssetList()
        {
            MetaverseEditorUtils.Box(() =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    DiscoverAssets();
                }

                if (GUILayout.Button("Select All", GUILayout.Width(80)))
                {
                    SelectAllAssets(true);
                }

                if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                {
                    SelectAllAssets(false);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // Filters
                DrawFilters();

                EditorGUILayout.Space(3);

                // Asset list header
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("", GUILayout.Width(20)); // Checkbox
                GUILayout.Label("Status", GUILayout.Width(60));
                GUILayout.Label("Name", GUILayout.Width(200));
                GUILayout.Label("Type", GUILayout.Width(100));
                GUILayout.Label("Path", GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                // Scrollable asset list
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

                var filteredAssets = GetFilteredAssets();
                foreach (var asset in filteredAssets)
                {
                    DrawAssetItem(asset);
                }

                EditorGUILayout.EndScrollView();

                // Summary
                var selectedCount = filteredAssets.Count(a => a.isSelected);
                EditorGUILayout.LabelField($"Selected: {selectedCount} / {filteredAssets.Count} (Total: {_allAssets.Count})", EditorStyles.miniLabel);
            });
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();

            // Search filter
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            var newSearchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.ExpandWidth(true));
            if (newSearchFilter != _searchFilter)
            {
                _searchFilter = newSearchFilter;
                SaveFilters();
            }

            // Clear search button
            if (!string.IsNullOrEmpty(_searchFilter) && GUILayout.Button("✕", GUILayout.Width(25)))
            {
                _searchFilter = "";
                SaveFilters();
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space(10);

            // Asset type filter
            EditorGUILayout.LabelField("Type:", GUILayout.Width(40));
            var newAssetTypeFilter = (AssetTypeFilter)EditorGUILayout.EnumPopup(_assetTypeFilter, GUILayout.Width(150));
            if (newAssetTypeFilter != _assetTypeFilter)
            {
                _assetTypeFilter = newAssetTypeFilter;
                SaveFilters();
            }

            EditorGUILayout.EndHorizontal();
        }

        private List<AssetItem> GetFilteredAssets()
        {
            var filtered = _allAssets.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                filtered = filtered.Where(a =>
                    a.assetName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    a.assetPath.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Apply asset type filter
            filtered = _assetTypeFilter switch
            {
                AssetTypeFilter.MetaSpacesOnly => filtered.Where(a => a.assetType == AssetItem.AssetType.MetaSpace),
                AssetTypeFilter.MetaPrefabsOnly => filtered.Where(a => a.assetType == AssetItem.AssetType.MetaPrefab),
                _ => filtered
            };

            return filtered.ToList();
        }

        private void DrawAssetItem(AssetItem asset)
        {
            EditorGUILayout.BeginHorizontal();

            // Checkbox
            var wasSelected = asset.isSelected;
            asset.isSelected = EditorGUILayout.Toggle(asset.isSelected, GUILayout.Width(20));
            if (asset.isSelected != wasSelected)
            {
                UpdateBatchSelection();
            }

            // Status icon
            var statusIcon = GetStatusIcon(asset.status);
            if (statusIcon != null)
            {
                GUILayout.Label(statusIcon, _statusIconStyle, GUILayout.Width(60), GUILayout.Height(18));
            }
            else
            {
                GUILayout.Label(asset.status.ToString(), GUILayout.Width(60));
            }

            // Name
            GUILayout.Label(asset.assetName, GUILayout.Width(200));

            // Type
            GUILayout.Label(asset.assetType.ToString(), GUILayout.Width(100));

            // Path
            GUILayout.Label(asset.assetPath, GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();

            // Error message if failed
            if (asset.status == AssetItem.BuildStatus.Failed && !string.IsNullOrEmpty(asset.errorMessage))
            {
                EditorGUILayout.HelpBox(asset.errorMessage, MessageType.Error);
            }
        }

        private void DrawSettings()
        {
            if (_currentBatch == null) return;

            MetaverseEditorUtils.Box(() =>
            {
                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                _currentBatch.stopOnFailure = EditorGUILayout.Toggle(
                    new GUIContent("Stop on Any Failure", "Stop the entire batch process if any single asset fails to build/upload"),
                    _currentBatch.stopOnFailure);
            });
        }

        private void DrawBuildControls()
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (_isBuilding)
                {
                    // Show progress
                    EditorGUILayout.LabelField($"Building: {_currentBuildAssetName}", EditorStyles.boldLabel);
                    EditorGUI.ProgressBar(
                        EditorGUILayout.GetControlRect(false, 20),
                        _buildProgress,
                        $"{_currentBuildIndex} / {_allAssets.Count(a => a.isSelected)}");

                    if (GUILayout.Button("Cancel", GUILayout.Height(30)))
                    {
                        CancelBuild();
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();

                    // Build & Upload button
                    var selectedCount = _allAssets.Count(a => a.isSelected);
                    MetaverseEditorUtils.Disabled(() =>
                    {
                        if (GUILayout.Button($"Build & Upload ({selectedCount} assets)", GUILayout.Height(30)))
                        {
                            StartBuild();
                        }
                    }, selectedCount == 0);

                    // Retry Failed button
                    var failedCount = _allAssets.Count(a => a.status == AssetItem.BuildStatus.Failed);
                    MetaverseEditorUtils.Disabled(() =>
                    {
                        if (GUILayout.Button($"Retry Failed ({failedCount} assets)", GUILayout.Height(30)))
                        {
                            RetryFailed();
                        }
                    }, failedCount == 0);

                    EditorGUILayout.EndHorizontal();
                }
            });
        }

        #endregion

        #region Initialization

        private void InitializeStyles()
        {
            _statusIconStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageOnly
            };

            // Load or create status icons
            _pendingIcon = CreateColorTexture(new Color(0.5f, 0.5f, 0.5f, 1f));
            _buildingIcon = CreateColorTexture(new Color(1f, 0.7f, 0f, 1f));
            _successIcon = CreateColorTexture(new Color(0f, 0.8f, 0f, 1f));
            _failedIcon = CreateColorTexture(new Color(0.8f, 0f, 0f, 1f));
        }

        private Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(16, 16);
            var pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D GetStatusIcon(AssetItem.BuildStatus status)
        {
            return status switch
            {
                AssetItem.BuildStatus.Pending => _pendingIcon,
                AssetItem.BuildStatus.Building => _buildingIcon,
                AssetItem.BuildStatus.Success => _successIcon,
                AssetItem.BuildStatus.Failed => _failedIcon,
                _ => null
            };
        }

        #endregion

        #region Asset Discovery

        private void DiscoverAssets()
        {
            _allAssets.Clear();

            // First, find all prefabs with MetaSpace component and cache their GUIDs
            // We need this to detect scenes that reference MetaSpace prefabs
            var metaSpacePrefabGuids = new HashSet<string>();
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var guid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(prefabPath)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                // Check if prefab has MetaSpace component - cache its GUID for scene detection
                if (prefab.GetComponent<MetaSpace>() != null)
                {
                    metaSpacePrefabGuids.Add(guid);
                }

                // Check if prefab has MetaPrefab component - add to asset list
                if (prefab.GetComponent<MetaPrefab>() != null)
                {
                    var assetItem = new AssetItem
                    {
                        assetPath = prefabPath,
                        assetName = prefab.name,
                        assetType = AssetItem.AssetType.MetaPrefab,
                        isSelected = false,
                        status = AssetItem.BuildStatus.Pending
                    };
                    _allAssets.Add(assetItem);
                }
            }

            // Find all MetaSpace assets (scenes with MetaSpace component)
            // We'll search for all scenes and check them more thoroughly
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var currentScenePath = EditorSceneManager.GetActiveScene().path;
            var scenesToRestore = new List<string>();

            // Save currently loaded scenes
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path))
                {
                    scenesToRestore.Add(scene.path);
                }
            }

            try
            {
                foreach (var guid in sceneGuids)
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(scenePath)) continue;

                    // Check if scene contains MetaSpace component (directly or via prefab reference)
                    if (SceneContainsMetaSpace(scenePath, metaSpacePrefabGuids))
                    {
                        var assetItem = new AssetItem
                        {
                            assetPath = scenePath,
                            assetName = System.IO.Path.GetFileNameWithoutExtension(scenePath),
                            assetType = AssetItem.AssetType.MetaSpace,
                            isSelected = false,
                            status = AssetItem.BuildStatus.Pending
                        };
                        _allAssets.Add(assetItem);
                    }
                }
            }
            finally
            {
                // Restore original scenes if they were changed
                if (scenesToRestore.Count > 0 && EditorSceneManager.GetActiveScene().path != currentScenePath)
                {
                    try
                    {
                        EditorSceneManager.OpenScene(scenesToRestore[0], OpenSceneMode.Single);
                        for (int i = 1; i < scenesToRestore.Count; i++)
                        {
                            EditorSceneManager.OpenScene(scenesToRestore[i], OpenSceneMode.Additive);
                        }
                    }
                    catch
                    {
                        // Ignore errors when restoring scenes
                    }
                }
            }

            // Sort by type then name
            _allAssets = _allAssets.OrderBy(a => a.assetType).ThenBy(a => a.assetName).ToList();

            // Restore selection from current batch
            RestoreBatchSelection();
        }

        private bool SceneContainsMetaSpace(string scenePath, HashSet<string> metaSpacePrefabGuids)
        {
            // Check if the scene file contains:
            // 1. A direct reference to the MetaSpace script (GUID: d6d49d5dd1953ec498f194bd8c73176b)
            // 2. A reference to any prefab that contains a MetaSpace component
            //
            // This handles both cases:
            // - MetaSpace component added directly to a GameObject in the scene
            // - MetaSpace prefab instantiated in the scene
            try
            {
                var sceneContents = System.IO.File.ReadAllText(scenePath);

                // Check for direct MetaSpace component reference
                if (sceneContents.Contains("d6d49d5dd1953ec498f194bd8c73176b"))
                {
                    return true;
                }

                // Check for references to MetaSpace prefabs
                // When a prefab is instantiated in a scene, the scene file contains the prefab's GUID
                foreach (var prefabGuid in metaSpacePrefabGuids)
                {
                    if (sceneContents.Contains(prefabGuid))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                // If we can't read the file, assume it doesn't contain MetaSpace
                return false;
            }
        }

        #endregion

        #region Filter Management

        private void LoadFilters()
        {
            _searchFilter = EditorPrefs.GetString(SearchFilterKey, "");
            _assetTypeFilter = (AssetTypeFilter)EditorPrefs.GetInt(AssetTypeFilterKey, 0);
        }

        private void SaveFilters()
        {
            EditorPrefs.SetString(SearchFilterKey, _searchFilter);
            EditorPrefs.SetInt(AssetTypeFilterKey, (int)_assetTypeFilter);
        }

        #endregion

        #region Batch Management

        private void LoadBatches()
        {
            _batches.Clear();
            _batchNames.Clear();

            // Load batch list
            var batchListJson = EditorPrefs.GetString(BatchListKey, "[]");
            try
            {
                var batchList = JsonUtility.FromJson<StringListWrapper>("{\"items\":" + batchListJson + "}");
                if (batchList?.items != null)
                {
                    _batchNames = batchList.items;
                }
            }
            catch
            {
                _batchNames = new List<string>();
            }

            // Load each batch configuration
            foreach (var batchName in _batchNames.ToList())
            {
                var batchJson = EditorPrefs.GetString(EditorPrefsPrefix + "Batch_" + batchName, "");
                if (!string.IsNullOrEmpty(batchJson))
                {
                    try
                    {
                        var batch = JsonUtility.FromJson<BatchConfiguration>(batchJson);
                        if (batch != null)
                        {
                            _batches[batchName] = batch;
                        }
                    }
                    catch
                    {
                        // Invalid batch, remove it
                        _batchNames.Remove(batchName);
                    }
                }
            }

            // Create default batch if none exist
            if (_batchNames.Count == 0)
            {
                CreateNewBatch("Default Batch");
            }

            // Load current batch
            _currentBatchName = EditorPrefs.GetString(CurrentBatchKey, _batchNames.FirstOrDefault());
            if (!_batchNames.Contains(_currentBatchName))
            {
                _currentBatchName = _batchNames.FirstOrDefault();
            }

            _currentBatch = _batches.ContainsKey(_currentBatchName) ? _batches[_currentBatchName] : null;
        }

        private void SaveBatches()
        {
            // Save batch list
            var batchListWrapper = new StringListWrapper { items = _batchNames };
            var batchListJson = JsonUtility.ToJson(batchListWrapper);
            EditorPrefs.SetString(BatchListKey, batchListJson.Substring(9, batchListJson.Length - 10)); // Remove wrapper

            // Save each batch configuration
            foreach (var kvp in _batches)
            {
                var batchJson = JsonUtility.ToJson(kvp.Value);
                EditorPrefs.SetString(EditorPrefsPrefix + "Batch_" + kvp.Key, batchJson);
            }

            // Save current batch
            if (!string.IsNullOrEmpty(_currentBatchName))
            {
                EditorPrefs.SetString(CurrentBatchKey, _currentBatchName);
            }
        }

        private void CreateNewBatch(string name = null)
        {
            var batchName = name ?? "New Batch " + (_batchNames.Count + 1);
            var counter = 1;
            var originalName = batchName;
            while (_batchNames.Contains(batchName))
            {
                batchName = originalName + " (" + counter + ")";
                counter++;
            }

            var newBatch = new BatchConfiguration { batchName = batchName };
            _batches[batchName] = newBatch;
            _batchNames.Add(batchName);

            SwitchToBatch(batchName);
            SaveBatches();
        }

        private void DeleteCurrentBatch()
        {
            if (_batchNames.Count <= 1)
            {
                EditorUtility.DisplayDialog("Cannot Delete", "You must have at least one batch.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Batch", $"Are you sure you want to delete the batch '{_currentBatchName}'?", "Delete", "Cancel"))
            {
                return;
            }

            _batches.Remove(_currentBatchName);
            _batchNames.Remove(_currentBatchName);
            EditorPrefs.DeleteKey(EditorPrefsPrefix + "Batch_" + _currentBatchName);

            SwitchToBatch(_batchNames.FirstOrDefault());
            SaveBatches();
        }

        private void SwitchToBatch(string batchName)
        {
            if (!_batches.ContainsKey(batchName)) return;

            _currentBatchName = batchName;
            _currentBatch = _batches[batchName];

            RestoreBatchSelection();
            SaveBatches();
        }

        private void RenameBatch(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || _batchNames.Contains(newName))
            {
                return;
            }

            var oldName = _currentBatchName;
            _batches[newName] = _currentBatch;
            _batches.Remove(oldName);
            _batchNames[_batchNames.IndexOf(oldName)] = newName;
            _currentBatchName = newName;
            _currentBatch.batchName = newName;

            EditorPrefs.DeleteKey(EditorPrefsPrefix + "Batch_" + oldName);
            SaveBatches();
        }

        private void RestoreBatchSelection()
        {
            if (_currentBatch == null) return;

            foreach (var asset in _allAssets)
            {
                asset.isSelected = _currentBatch.selectedAssetPaths.Contains(asset.assetPath);
            }
        }

        private void UpdateBatchSelection()
        {
            if (_currentBatch == null) return;

            _currentBatch.selectedAssetPaths = _allAssets
                .Where(a => a.isSelected)
                .Select(a => a.assetPath)
                .ToList();

            SaveBatches();
        }

        private void SelectAllAssets(bool selected)
        {
            foreach (var asset in _allAssets)
            {
                asset.isSelected = selected;
            }
            UpdateBatchSelection();
        }

        [Serializable]
        private class StringListWrapper
        {
            public List<string> items;
        }

        #endregion

        #region Build Process

        private void StartBuild()
        {
            if (_isBuilding) return;

            var selectedAssets = _allAssets.Where(a => a.isSelected).ToList();
            if (selectedAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("No Assets Selected", "Please select at least one asset to build.", "OK");
                return;
            }

            // Reset status for selected assets
            foreach (var asset in selectedAssets)
            {
                asset.status = AssetItem.BuildStatus.Pending;
                asset.errorMessage = null;
            }

            _isBuilding = true;
            _currentBuildIndex = 0;

            BuildNextAsset(selectedAssets);
        }

        private void RetryFailed()
        {
            if (_isBuilding) return;

            var failedAssets = _allAssets.Where(a => a.status == AssetItem.BuildStatus.Failed).ToList();
            if (failedAssets.Count == 0) return;

            // Reset status for failed assets
            foreach (var asset in failedAssets)
            {
                asset.status = AssetItem.BuildStatus.Pending;
                asset.errorMessage = null;
            }

            _isBuilding = true;
            _currentBuildIndex = 0;

            BuildNextAsset(failedAssets);
        }

        private void CancelBuild()
        {
            _isBuilding = false;
            _currentBuildIndex = 0;
            _buildProgress = 0;
            _currentBuildAssetName = "";

            Debug.Log("[Batch Builder] Build cancelled by user.");
        }

        private void BuildNextAsset(List<AssetItem> assetsToBuild)
        {
            if (!_isBuilding || _currentBuildIndex >= assetsToBuild.Count)
            {
                // Build complete
                _isBuilding = false;
                _buildProgress = 1f;

                var successCount = assetsToBuild.Count(a => a.status == AssetItem.BuildStatus.Success);
                var failedCount = assetsToBuild.Count(a => a.status == AssetItem.BuildStatus.Failed);

                Debug.Log($"[Batch Builder] Build complete. Success: {successCount}, Failed: {failedCount}");
                EditorUtility.DisplayDialog("Build Complete",
                    $"Batch build completed.\n\nSuccess: {successCount}\nFailed: {failedCount}", "OK");

                Repaint();
                return;
            }

            var asset = assetsToBuild[_currentBuildIndex];
            _currentBuildAssetName = asset.assetName;
            _buildProgress = (float)_currentBuildIndex / assetsToBuild.Count;

            asset.status = AssetItem.BuildStatus.Building;
            Repaint();

            Debug.Log($"[Batch Builder] Building {asset.assetType}: {asset.assetName} ({_currentBuildIndex + 1}/{assetsToBuild.Count})");

            // Build based on asset type
            if (asset.assetType == AssetItem.AssetType.MetaSpace)
            {
                BuildMetaSpace(asset, () =>
                {
                    _currentBuildIndex++;
                    BuildNextAsset(assetsToBuild);
                }, error =>
                {
                    asset.status = AssetItem.BuildStatus.Failed;
                    asset.errorMessage = error?.ToString() ?? "Unknown error";
                    Debug.LogError($"[Batch Builder] Failed to build {asset.assetName}: {asset.errorMessage}");

                    if (_currentBatch.stopOnFailure)
                    {
                        _isBuilding = false;
                        EditorUtility.DisplayDialog("Build Failed",
                            $"Build stopped due to failure:\n\n{asset.assetName}\n\n{asset.errorMessage}", "OK");
                        Repaint();
                        return;
                    }

                    _currentBuildIndex++;
                    BuildNextAsset(assetsToBuild);
                });
            }
            else if (asset.assetType == AssetItem.AssetType.MetaPrefab)
            {
                BuildMetaPrefab(asset, () =>
                {
                    _currentBuildIndex++;
                    BuildNextAsset(assetsToBuild);
                }, error =>
                {
                    asset.status = AssetItem.BuildStatus.Failed;
                    asset.errorMessage = error?.ToString() ?? "Unknown error";
                    Debug.LogError($"[Batch Builder] Failed to build {asset.assetName}: {asset.errorMessage}");

                    if (_currentBatch.stopOnFailure)
                    {
                        _isBuilding = false;
                        EditorUtility.DisplayDialog("Build Failed",
                            $"Build stopped due to failure:\n\n{asset.assetName}\n\n{asset.errorMessage}", "OK");
                        Repaint();
                        return;
                    }

                    _currentBuildIndex++;
                    BuildNextAsset(assetsToBuild);
                });
            }
        }

        private void BuildMetaSpace(AssetItem asset, Action onSuccess, Action<object> onError)
        {
            // Load the scene first
            var scene = EditorSceneManager.OpenScene(asset.assetPath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                onError?.Invoke("Failed to load scene");
                return;
            }

            var metaSpace = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MetaSpace>(true)).FirstOrDefault();
            if (metaSpace == null)
            {
                onError?.Invoke("Scene does not contain a MetaSpace component");
                return;
            }

            // Get supported platforms from MetaSpace
            var platforms = metaSpace.SupportedPlatforms;
            if (platforms == 0)
            {
                platforms = Platform.StandaloneWindows64 | Platform.Android | Platform.iOS | Platform.WebGL;
            }

            // Build the scene
            scene.BuildStreamedScene(
                platforms,
                builds =>
                {
                    asset.status = AssetItem.BuildStatus.Success;
                    FinishBuildAndUploadScene(asset, onSuccess, onError, builds, out scene, out metaSpace);
                },
                platformOptions: null,
                failed: onError);
        }

        private void BuildMetaPrefab(AssetItem asset, Action onSuccess, Action<object> onError)
        {
            // Load the prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset.assetPath);
            if (prefab == null)
            {
                onError?.Invoke("Failed to load prefab");
                return;
            }

            // Find MetaPrefab component
            var metaPrefab = prefab.GetComponent<MetaPrefab>();
            if (metaPrefab == null)
            {
                onError?.Invoke("Prefab does not contain a MetaPrefab component");
                return;
            }

            // Get supported platforms from MetaPrefab
            var platforms = metaPrefab.SupportedPlatforms;
            if (platforms == 0)
            {
                platforms = Platform.StandaloneWindows64 | Platform.Android | Platform.iOS | Platform.WebGL;
            }

            // Build the prefab
            prefab.BuildPrefab(
                platforms,
                builds =>
                {
                    asset.status = AssetItem.BuildStatus.Success;
                    FinishBuildAndUploadPrefab(asset, onSuccess, onError, builds, out prefab, out metaPrefab);
                },
                onPreProcessBuild: null,
                platformOptions: null,
                failed: onError);
        }

        private static void FinishBuildAndUploadScene(
            AssetItem asset,
            Action onSuccess,
            Action<object> onError,
            IEnumerable<MetaverseAssetBundleAPI.BundleBuild> builds,
            out Scene scene,
            out MetaSpace metaSpace)
        {
            scene = EditorSceneManager.OpenScene(asset.assetPath, OpenSceneMode.Single);
            metaSpace = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MetaSpace>(true)).FirstOrDefault();
            Editor editor = Editor.CreateEditor(metaSpace, typeof(MetaSpaceEditor));
            var form = ((MetaSpaceEditor)editor).GetUpsertForm(metaSpace.ID, metaSpace, true);
            ((MetaSpaceEditor)editor).Init();
            ((MetaSpaceEditor)editor).UploadBundles(MetaverseProgram.ApiClient.MetaSpaces, scene.path, builds, form, (_, _) => onSuccess(), onError, suppressDialog: true);
        }

        private static void FinishBuildAndUploadPrefab(
            AssetItem asset,
            Action onSuccess,
            Action<object> onError,
            IEnumerable<MetaverseAssetBundleAPI.BundleBuild> builds,
            out GameObject prefab,
            out MetaPrefab metaPrefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset.assetPath);
            metaPrefab = prefab.GetComponent<MetaPrefab>();
            Editor editor = Editor.CreateEditor(metaPrefab, typeof(MetaPrefabEditor));
            var form = ((MetaPrefabEditor)editor).GetUpsertForm(metaPrefab.ID, metaPrefab, true);
            ((MetaPrefabEditor)editor).Init();
            ((MetaPrefabEditor)editor).UploadBundles(MetaverseProgram.ApiClient.Prefabs, asset.assetPath, builds, form, (_, _) => onSuccess(), onError, suppressDialog: true);
        }

        #endregion
    }
}

