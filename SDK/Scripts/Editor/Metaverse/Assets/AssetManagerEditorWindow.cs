using System;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class AssetManagerEditorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        
        public enum Tabs
        {
            MetaPrefab,
            MetaSpace,
            LandPlot,
        }

        private Tabs _currentTab;
        private string[] _tabNames;
        private AssetPaginatedEditor<PrefabDto, PrefabQueryParams> _metaPrefabPaginated;
        private AssetPaginatedEditor<MetaSpaceDto, MetaSpaceQueryParams> _metaSpacePaginated;
        private AssetPaginatedEditor<LandPlotDto, LandPlotQueryParams> _landPlotPaginated;


        [MenuItem(MetaverseConstants.MenuItems.WindowsMenuRootPath + "Asset Manager")]
        public static void Open()
        {
            var window = GetWindow<AssetManagerEditorWindow>();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Asset Manager", MetaverseEditorUtils.EditorIcon);
        }

        private void OnGUI()
        {
            _tabNames ??= Enum.GetNames(typeof(Tabs));

            MetaverseEditorUtils.Header("Asset Manager");

            MetaverseEditorUtils.DrawLoadingScreen(() =>
            {
                if (!MetaverseProgram.ApiClient.Account.IsLoggedIn)
                {
                    EditorGUILayout.HelpBox("Please log in to view your assets.", MessageType.Info);
                    if (GUILayout.Button("Log In"))
                        MetaverseAccountWindow.Open();
                    return;
                }

                _currentTab = (Tabs)GUILayout.Toolbar((int)_currentTab, _tabNames, EditorStyles.toolbarButton);

                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

                try
                {
                    switch (_currentTab)
                    {
                        case Tabs.MetaPrefab:
                            _metaPrefabPaginated ??= new AssetPaginatedEditor<PrefabDto, PrefabQueryParams>(
                                "Meta Prefabs", 
                                MetaverseProgram.ApiClient.Prefabs, 
                                MetaverseProgram.ApiClient.Prefabs)
                            {
                                Icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Prefab Icon" : "Prefab Icon").image
                            };
                            _metaPrefabPaginated?.Draw();
                            break;
                        case Tabs.MetaSpace:
                            _metaSpacePaginated ??= new AssetPaginatedEditor<MetaSpaceDto, MetaSpaceQueryParams>(
                                "Meta Spaces", 
                                MetaverseProgram.ApiClient.MetaSpaces, 
                                MetaverseProgram.ApiClient.MetaSpaces)
                            {
                                Icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_ToolHandleGlobal" : "ToolHandleGlobal").image,
                                QueryParamsModifier = q =>
                                {
                                    q.SortMode = MetaSpaceSortMode.Latest;
                                }
                            };
                            _metaSpacePaginated?.Draw();
                            break;
                        case Tabs.LandPlot:
                            _landPlotPaginated ??= new AssetPaginatedEditor<LandPlotDto, LandPlotQueryParams>(
                                "Land Plots",
                                MetaverseProgram.ApiClient.Land,
                                MetaverseProgram.ApiClient.Land)
                            {
                                Icon = EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSculpt On").image
                            };
                            _landPlotPaginated?.Draw();
                            break;
                    }
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }

            }, MetaverseEditorUtils.DrawDefaultLoadingScreen, !MetaverseProgram.Initialized);
        }

        private void Update()
        {
            Repaint();
        }
    }
}
