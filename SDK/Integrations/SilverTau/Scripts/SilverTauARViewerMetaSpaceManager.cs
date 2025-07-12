using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Common.Models.QueryParams;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using MetaverseCloudEngine.Unity.Async;
using TMPro;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    [HideMonoScript]
    public class SilverTauARViewerMetaSpaceManager : TriInspectorMonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private GameObject roomPlanUnityKit;
        [SerializeField] private GameObject capturedRoomSnapshot;
        [SerializeField] private GameObject placeObjectAR;

        [Space(10)]
        [Header("Common")]
        [SerializeField] private string nameScanDirectory = "model-viewer";
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera aRCamera;
        [SerializeField] private GameObject envObjects;
        
        [Space(10)]
        [Header("UI")]
        [SerializeField] private GameObject uIManager;
        [SerializeField] private GameObject loader;
        
        [Space(10)]
        [Header("Pop-ups")]
        [SerializeField] private GameObject popupHasScanName;
        [SerializeField] private GameObject popupInputScanName;
        [SerializeField] private TMP_InputField inputFieldScanName;

        /// <summary>
        /// A method that controls the pop-up for entering a scan name.
        /// </summary>
        /// <param name="status">Popup status parameter.</param>
        public void CallPopupInputScanName(bool status)
        {
            if(!popupInputScanName) return;
            popupInputScanName.SetActive(status);
        }
        
        /// <summary>
        /// A function that saves a scan from a pop-up window.
        /// </summary>
        public void SaveExperienceFromPopup()
        {
            if(inputFieldScanName == null) return;
            if(string.IsNullOrEmpty(inputFieldScanName.text)) return;
            SaveExperience(inputFieldScanName.text);
        }
        
        /// <summary>
        /// A function that stores scanned experience, scan.
        /// </summary>
        /// <param name="value">Scan name.</param>
        private void SaveExperience(string value)
        {
            try
            {
                var pathToScans = Path.Combine(Application.persistentDataPath, nameScanDirectory);
                if (!Directory.Exists(pathToScans))
                    Directory.CreateDirectory(pathToScans);
                if (Directory.Exists(pathToScans))
                {
                    var dirs = Directory.GetDirectories(pathToScans);
                    if (dirs.Any())
                    {
                        foreach (var dir in dirs)
                        {
                            if(string.IsNullOrEmpty(dir)) continue;
                            var dirName = Path.GetFileName(dir);
                            if (dirName != value) continue;
                            popupHasScanName.SetActive(true);
                            Debug.Log("A scan with this name already exists.");
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("An error occurred while saving a scan.");
                Debug.Log(e);
                return;
            }
            
            StartCoroutine(IESaveExperience(value));
        }
        
        /// <summary>
        /// A Coroutine that helps to save a scan correctly.
        /// </summary>
        /// <param name="value">Scan name.</param>
        /// <returns></returns>
        private IEnumerator IESaveExperience(string value)
        {
            loader.gameObject.SetActive(true);
            yield return IECustomSave(value);
            yield return new WaitForSeconds(0.5f);
            roomPlanUnityKit.SendMessage("StopCaptureSession");
            yield return new WaitForSeconds(0.2f);
            roomPlanUnityKit.SendMessage("Dispose");
            yield return new WaitForSeconds(0.2f);
            capturedRoomSnapshot.SendMessage("Dispose", (Action)(() =>
            {
                if(mainCamera) mainCamera.gameObject.SetActive(true);
                if(aRCamera) aRCamera.gameObject.SetActive(false);
                if(envObjects) envObjects.gameObject.SetActive(false);
                if(placeObjectAR) placeObjectAR.gameObject.SetActive(false);
                CallPopupInputScanName(false);
                uIManager.SendMessage("OpenMenu", "scans");
                loader.gameObject.SetActive(false);
            }));
        }
        
        private IEnumerator IECustomSave(string value)
        {
            yield return UniTask.Create(async cancellationToken =>
            {
                if (!MetaverseProgram.ApiClient.Account.IsLoggedIn)
                    return;
                
                var spaces = (await (await MetaverseProgram.ApiClient.MetaSpaces.GetAllAsync(
                    new MetaSpaceQueryParams
                    {
                        Count = 1,
                        NameFilter = value,
                        Writeable = true,
                        ContributorName = MetaverseProgram.ApiClient.Account.CurrentUser.UserName,
                        ContentType = AssetContentType.Bundle,
                        AdvancedSearch = false,
                        HasSourceLandPlot = true,

                    })).GetResultAsync())?.ToArray();
                
                var space = spaces?.FirstOrDefault();
                var landplot = envObjects.GetComponent<LandPlot>();
                if (landplot is null)
                    return;

                landplot.ID = space?.SourceLandPlotId;
                landplot.name = $"ENV_SCAN: \"{space?.Name ?? value}\"";

                var finished = false;
                landplot.events.onSaveFinished.AddListener(OnLandPlotSaveFinished);
                landplot.Save();
                
                await UniTask.WaitUntil(() => finished, PlayerLoopTiming.Update, cancellationToken);
                return;

                void OnLandPlotSaveFinished()
                {
                    landplot.events.onSaveFinished.RemoveListener(OnLandPlotSaveFinished);
                    var id = landplot.ID;
                    if (id.HasValue)
                    {
                        MetaverseProgram.ApiClient.MetaSpaces.UpsertAsync(new MetaSpaceUpsertForm { Id = id.Value, Name = value, SourceLandPlotId = landplot.ID })
                            .ResponseThen(_ =>
                            {
                                finished = true;
                                MetaverseProgram.Logger.Log($"MetaSpace '{value}' saved successfully with ID: {id.Value}");
                            }, e =>
                            {
                                MetaverseProgram.Logger.LogError(e);
                                finished = true;
                            }, cancellationToken: cancellationToken);
                        return;
                    }

                    finished = true;
                }
            }, destroyCancellationToken).ToCoroutine();
        }
    }
}