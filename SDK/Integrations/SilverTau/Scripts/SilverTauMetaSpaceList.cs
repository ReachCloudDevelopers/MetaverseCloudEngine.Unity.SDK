using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.UI.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    /// <summary>
    /// Renders a list of MetaSpaces as scans in the SilverTau integration.
    /// </summary>
    public class SilverTauMetaSpaceList : TriInspectorMonoBehaviour
    {
        [SerializeField]
        private Transform container;
        [SerializeField]
        private SilverTauMetaSpaceListItem itemPrefab;
        [SerializeField]
        private GameObject noItemsGameObject;
        
        private readonly List<SilverTauMetaSpaceListItem> _items = new ();
        private bool _isRepainting;

        private void OnEnable()
        {
            RepaintList();
        }

        private void Clear()
        {
            foreach (var item in _items.ToArray())
            {
                if (!item) continue;
                Destroy(item.gameObject);
            }

            UpdateRectTransforms();
        }

        private void RepaintList()
        {
            Clear();
            
            if (noItemsGameObject)
                noItemsGameObject.SetActive(false);
            
            MetaverseProgram.ApiClient.MetaSpaces.GetAllAsync(
                new MetaSpaceQueryParams
                {
                    NameFilter = "ENV_SCAN:",
                    AdvancedSearch = false,
                    HasSourceLandPlot = true,
                    ContentType = AssetContentType.Bundle,
                    Count = 100,
                    ContributorName = MetaverseProgram.ApiClient.Account.CurrentUser.UserName,
					Writeable = true,

                }).ResponseThen(r =>
            {
                if (!this || !isActiveAndEnabled)
                    return;

                if (!container)
                {
                    MetaverseProgram.Logger.LogError("[SilverTauMetaSpaceList] Container is not set.");
                    return;
                }

                _isRepainting = true;
                
                var spaces = r.OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate).ToArray();
                if (noItemsGameObject)
                    noItemsGameObject.SetActive(!spaces.Any());

                foreach (var dto in spaces)
                {
                    var instance = Instantiate(itemPrefab.gameObject, container)
                        .GetComponent<SilverTauMetaSpaceListItem>();
                    if (!instance)
                    {
                        MetaverseProgram.Logger.LogError("[SilverTauMetaSpaceList] Item prefab is not set.");
                        continue;
                    }
                    instance.Repaint(dto, this);
                    _items.Add(instance);
                    _isRepainting = false;
                }
                
                UpdateRectTransforms();
            }, e =>
            {
                if (!this || !isActiveAndEnabled)
                    return;
                
                if (noItemsGameObject)
                    noItemsGameObject.SetActive(true);

                UpdateRectTransforms();
                
                _isRepainting = true;
            });
        }

        private void UpdateRectTransforms()
        {
            if (!container)
            {
                MetaverseProgram.Logger.LogError("[SilverTauMetaSpaceList] Container is not set.");
                return;
            }
            
            var layoutHelpers = container.GetComponentsInChildren<LayoutHelper>(true);
            foreach (var layoutHelper in layoutHelpers)
                layoutHelper.Layout();
        }

        /// <summary>
        /// Called when a delete request is made for a MetaSpace item.
        /// </summary>
        /// <param name="item">The MetaSpace list item to delete.</param>
        public void OnDeleteRequested(SilverTauMetaSpaceListItem item)
        {
            MetaverseProgram.ApiClient.MetaSpaces.DeleteAsync(item.MetaSpace.Id)
                .ResponseThen(() =>
                {
                    if (!this || !isActiveAndEnabled || !item)
                        return;
                    if (!_items.Remove(item))
                        return;
                    Destroy(item.gameObject);
                    if (!_items.Any() && noItemsGameObject)
                        noItemsGameObject.SetActive(true);
                    UpdateRectTransforms();
                }, e =>
                {
                    MetaverseProgram.Logger.LogError($"[SilverTauMetaSpaceList] Failed to delete MetaSpace: {e}");
                });
        }
    }
}