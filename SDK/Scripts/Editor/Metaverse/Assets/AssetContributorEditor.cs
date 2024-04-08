using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class AssetContributorEditor<TAssetDto> where TAssetDto : AssetDto
    {
        private readonly PaginatedEditor<AssetContributorDto> _contributorPager;

        private AssetDto _assetDto;
        private bool _isRequestingAsset;
        private string _assetRequestError;

        public AssetContributorEditor(IAssetReference asset, IAssetController<TAssetDto> assetController, IAssetContributorController contributorController)
        {
            Asset = asset;
            AssetController = assetController;
            ContributorController = contributorController;

            _contributorPager = new PaginatedEditor<AssetContributorDto>("Contributors");
            _contributorPager.AddButtonClicked += OnContributorViewAddButtonClicked;
            _contributorPager.BeginRequest += OnContributorViewBeginRequest;
            _contributorPager.DrawRecord += OnContributorViewDrawRecord;
        }

        public IAssetReference Asset { get; set; }
        public IAssetController<TAssetDto> AssetController { get; }
        public IAssetContributorController ContributorController { get; }

        public void Draw()
        {
            if (Asset == null)
                return;

            if (!MetaverseProgram.Initialized)
                return;

            if (!MetaverseProgram.ApiClient.Account.IsLoggedIn || Asset.ID == null)
            {
                TriggerRefresh();
                return;
            }

            if (!string.IsNullOrEmpty(_assetRequestError))
            {
                EditorGUILayout.HelpBox(_assetRequestError, MessageType.Error);
                return;
            }

            if (_isRequestingAsset)
            {
                return;
            }

            _contributorPager.Draw();

            RequestAsset();
        }

        private void OnAssetRequest(TAssetDto assetDto)
        {
            this._assetDto = assetDto;
            _isRequestingAsset = false;
        }

        private void OnAssetRequestError(object e)
        {
            _isRequestingAsset = false;
            _assetRequestError = e.ToString();
        }

        private void RequestAsset()
        {
            if (MetaverseProgram.ApiClient.Account.IsLoggedIn && _assetDto == null && !_isRequestingAsset && Asset.ID != null)
            {
                _isRequestingAsset = true;
                AssetController.FindAsync(Asset.ID.Value).ResponseThen(OnAssetRequest, OnAssetRequestError);
            }
        }

        private void OnContributorViewAddButtonClicked()
        {
            if (Asset.ID != null)
            {
                AssetContributorFormEditor.New(Asset.ID.Value, ContributorController, c =>
                {
                    _contributorPager.Refresh();
                });
            }
        }

        private bool OnContributorViewBeginRequest(int offset, int count, string filter)
        {
            if (Asset.ID == null)
                return false;

            ContributorController.GetContributorsAsync(new AssetContributorQueryParams
            {
                Id = Asset.ID.Value,
                Count = (uint)count,
                Offset = (uint)offset,
                NameFilter = filter, 
                QueryInvites = true,

            }).ResponseThen(contributors =>
            {
                _contributorPager.EndRequest(contributors);
            },
            e => _contributorPager.RequestError = e.ToString());

            return true;
        }

        private bool OnContributorViewDrawRecord(AssetContributorDto record)
        {
            MetaverseEditorUtils.Box(() =>
            {
                if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(23)))
                    AssetContributorFormEditor.Edit(record, ContributorController, c =>
                    {
                        _contributorPager.Refresh();
                    });

                GUILayout.Space(10);

                if (record.SystemUser != null)
                    EditorGUILayout.LabelField(record.SystemUser.UserName + (!record.HasAcceptedInvite ? " (Invite Sent)" : ""));
                else if (record.Organization != null)
                    EditorGUILayout.LabelField(record.Organization.Name + (!record.HasAcceptedInvite ? " (Invite Sent)" : ""));

                MetaverseEditorUtils.Disabled(() => EditorGUILayout.EnumPopup(record.Role, GUILayout.Width(100)));

            }, vertical: false);
            return true;
        }

        private void TriggerRefresh()
        {
            _assetDto = null;
            _assetRequestError = null;
            _isRequestingAsset = false;
        }
    }
}
