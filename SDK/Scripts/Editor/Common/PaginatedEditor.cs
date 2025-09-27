using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class PaginatedEditor<T>
    {
        public delegate bool BeginRequestDelegate(int offset, int count, string filter);
        public delegate bool DrawRecordDelegate(T record);

        private bool _allowNextPage;
        private bool _allowPrevPage;
        private bool _isLoading;
        private bool _requestAlreadyFinished;
        private bool _requesting;
        private Vector2 _scrollPos;
        private IEnumerable<T> _data;
        private bool _delayedRequest;
        private int _requestedPage = 1;
        private bool _isError;
        private readonly bool _collapsable;
        private bool _isExpanded;

        private static GUIContent RefreshContent;
        private static GUIContent AddContent;
        private static GUIStyle _collapsableHeaderStyle;

        private static GUIStyle CollapsableHeaderStyle => _collapsableHeaderStyle ??= new GUIStyle(EditorStyles.foldoutHeader)
        {
            fontStyle = FontStyle.Bold
        };

        public PaginatedEditor(GUIContent title, bool collapsable = false)
        {
            RefreshContent ??= EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Refresh" : "Refresh");
            AddContent ??= EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_CreateAddNew" : "CreateAddNew");
            Title = title;
            _collapsable = collapsable;
            _isExpanded = !collapsable;
        }

        public PaginatedEditor(string title, bool collapsable = false) : this(new GUIContent(title), collapsable)
        {
        }

        public event Action AddButtonClicked;
        public event BeginRequestDelegate BeginRequest;
        public event DrawRecordDelegate DrawRecord;

        public int PageIndex { get; private set; }
        public int PageSize { get; private set; } = 25;
        public string Filter { get; private set; }
        public string RequestError { get; set; }

        public bool DisplayPagers { get; set; } = true;
        public bool DisplayFilter { get; set; } = true;
        public bool DisplayAddButton { get; set; } = true;
        public bool DisplayRefreshButton { get; set; } = true;

        public GUIContent Title { get; set; }

        public void Draw(float? maxHeight = null)
        {
            MetaverseEditorUtils.Box(() =>
            {
                DrawToolbar();

                if (_collapsable && !_isExpanded)
                    return;

                if (!string.IsNullOrEmpty(RequestError))
                {
                    if (!_isError)
                    {
                        _requesting = false;
                        _isError = true;
                    }
                    
                    EditorGUILayout.HelpBox(RequestError, MessageType.Error);
                    TryRefresh();
                    return;
                }

                _isError = false;

                if (Event.current.type == EventType.Layout)
                    _isLoading = _requesting;

                if (DisplayFilter)
                {
                    var newFilter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField);
                    if (newFilter != Filter)
                    {
                        Filter = newFilter;

                        PageIndex = 0;
                        _requestedPage = PageIndex + 1;

                        if (_requesting)
                        {
                            _delayedRequest = true;
                        }
                        else
                        {
                            Refresh();
                        }
                    }
                }

                MetaverseEditorUtils.DrawLoadingScreen(() =>
                {
                    if (TryRefresh()) return;

                    if (!_data.Any())
                    {
                        EditorGUILayout.HelpBox("No records to display.", MessageType.Info);

                        if (DisplayPagers)
                        {
                            DrawPager();
                        }
                        return;
                    }

                    _scrollPos = maxHeight != null ? 
                        EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(maxHeight.Value), GUILayout.ExpandHeight(false)) : 
                        EditorGUILayout.BeginScrollView(_scrollPos);

                    foreach (var record in _data.ToArray())
                    {
                        if (DrawRecord?.Invoke(record) == false)
                            GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndScrollView(); 

                    if (DisplayPagers)
                    {
                        DrawPager();
                    }

                }, MetaverseEditorUtils.DrawDefaultLoadingScreen, _isLoading);
            });

            _requestAlreadyFinished = false;
        }

        private bool TryRefresh()
        {
            if (_data == null)
            {
                if (BeginRequest != null && BeginRequest.Invoke(PageSize * PageIndex, PageSize + 1, Filter))
                {
                    if (!_requestAlreadyFinished)
                    {
                        _requesting = true;
                        return true;
                    }
                }

                if (_data == null)
                    return true;
            }

            return false;
        }

        public void NextPage()
        {
            if (!_allowNextPage)
                return;

            PageIndex++;
            _requestedPage = PageIndex + 1;

            _data = null;
            _scrollPos = Vector2.zero;
        }

        public void PreviousPage()
        {
            if (!_allowPrevPage)
                return;

            if (PageIndex > 0)
            {
                PageIndex--;
                _requestedPage = PageIndex + 1;
            }

            _data = null;
            _scrollPos = Vector2.zero;
        }

        public void Refresh()
        {
            RequestError = null;

            if (_requesting)
            {
                _delayedRequest = true;
                return;
            }

            _data = null;
        }

        public void EndRequest(IEnumerable<T> data)
        {
            var dataArr = data as T[] ?? data?.ToArray() ?? Array.Empty<T>();
            
            if (!_requesting) 
                _requestAlreadyFinished = true;

            _requesting = false;
            _allowNextPage = dataArr.Length >= PageSize + 1;
            _allowPrevPage = PageIndex > 0;
            
            _data = dataArr.Take(PageSize);

            if (_delayedRequest)
            {
                Refresh();
                _delayedRequest = false;
            }
        }

        public void RemoveRecord(T record)
        {
            _data = _data.Where(x => !x.Equals(record));
        }

        private void DrawToolbar()
        {
            var exitGUI = false;

            try
            {
                EditorGUILayout.BeginHorizontal("toolbar");
                if (_collapsable)
                    _isExpanded = GUILayout.Toggle(_isExpanded, Title, CollapsableHeaderStyle, GUILayout.ExpandWidth(true));
                else
                    EditorGUILayout.LabelField(Title, EditorStyles.boldLabel);

                if (_requesting)
                    GUI.enabled = false;

                const int toolbarButtonsWidth = 30;
                const int toolbarButtonsHeight = 18;

                if (DisplayAddButton && GUILayout.Button(AddContent, EditorStyles.toolbarButton, GUILayout.Width(toolbarButtonsWidth), GUILayout.Height(toolbarButtonsHeight)))
                {
                    exitGUI = true;
                    AddButtonClicked?.Invoke();
                    return;
                }

                if (DisplayRefreshButton && GUILayout.Button(RefreshContent, EditorStyles.toolbarButton,
                        GUILayout.Width(toolbarButtonsWidth), GUILayout.Height(toolbarButtonsHeight)))
                {
                    _data = null;
                    RequestError = null;
                }
            }
            finally
            {
                if (exitGUI)
                    GUIUtility.ExitGUI();
                else
                {
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();   
                }
            }
        }

        private void DrawPager()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _allowPrevPage;
            if (GUILayout.Button("Prev"))
                PreviousPage();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            _requestedPage = EditorGUILayout.IntField(_requestedPage, EditorStyles.centeredGreyMiniLabel);
            if (Event.current != null && Event.current.keyCode == KeyCode.Return)
            {
                if (_requestedPage != PageIndex + 1)
                {
                    PageIndex = Mathf.Max(0, _requestedPage - 1);
                    _requestedPage = PageIndex + 1;
                    _scrollPos = Vector2.zero;
                    Refresh();
                }
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = _allowNextPage;
            if (GUILayout.Button("Next"))
                NextPage();
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
    }
}
