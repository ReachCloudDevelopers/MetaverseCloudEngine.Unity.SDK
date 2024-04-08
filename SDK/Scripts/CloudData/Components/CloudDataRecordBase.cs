using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using MetaverseCloudEngine.Unity.CloudData.Attributes;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

#pragma warning disable CS0414

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    /// <summary>
    /// A base class for cloud data records. This class is responsible for handling the saving and loading of data to the cloud.
    /// </summary>
    /// <typeparam name="T">The type of data to be saved and loaded.</typeparam>
    [HideMonoScript]
    public abstract partial class CloudDataRecordBase<T> : TriInspectorMonoBehaviour, ICloudDataRecord
    {
        /// <summary>
        /// A class that contains the blockchain source and type.
        /// </summary>
        [Serializable]
        public class BlockchainSourceIdentifier
        {
            public string blockchainSource;
            public BlockchainType blockchainType;
        }

        /// <summary>
        /// A class that contains options for land plots.
        /// </summary>
        [Serializable]
        public class LandPlotOptions
        {
            public bool appendBuildableIDToKey = true;
            public bool deleteOnBuildableRemoved = true;
        }

        /// <summary>
        /// A class that contains events for the cloud data record.
        /// </summary>
        [Serializable]
        public class EventCallbacks
        {
            [Header("Save")] [Tooltip("Invoked when the save process begins.")]
            public UnityEvent onSaveBegin = new();

            [Tooltip("Invoked when the save process succeeds.")]
            public UnityEvent onSaveSuccess = new();

            [Tooltip(
                "Invoked when the save process finishes. This is invoked after the save success or save failed events.")]
            public UnityEvent onSaveFinished = new();

            [Tooltip("Invoked when the save process fails.")]
            public UnityEvent onSaveFailed = new();

            [Header("Load")] [Tooltip("Invoked when the load process begins.")]
            public UnityEvent onLoadBegin = new();

            [Tooltip("Invoked when the load process succeeds.")]
            public UnityEvent onLoadSuccess = new();

            [Tooltip(
                "Invoked when the load process finishes. This is invoked after the load success or load failed events.")]
            public UnityEvent onLoadFinished = new();

            [Tooltip("Invoked when the load process fails.")]
            public UnityEvent onLoadFailed = new();
        }

        [Tooltip("The record template ID. This is the ID of the record template that the data will be saved to.")]
        [CloudDataRecordTemplateId]
        public string recordTemplate;

        [Tooltip("The key of the data. This is the key that the data will be saved to.")] [SerializeField]
        private string key;

        [Tooltip("Whether or not to display errors when saving data.")] [SerializeField]
        private bool displaySaveErrors = true;

        [Tooltip("Whether or not to display errors when loading data.")] [SerializeField]
        private bool displayLoadErrors;

        [Tooltip("Whether or not to load the data on start.")] [SerializeField]
        private bool loadOnStart = true;

        [Tooltip("The blockchain source and type.")] [SerializeField]
        private BlockchainSourceIdentifier blockchain = new();

        [Tooltip("The land plot options.")] [SerializeField]
        private LandPlotOptions landPlotOptions = new();

        [Tooltip("The events for the cloud data record.")] [SerializeField]
        private EventCallbacks events = new();

        private bool _hasStarted;

        /// <summary>
        /// The record template ID. This is the ID of the record template that the data will be saved to.
        /// </summary>
        public LandPlotOptions LandOptions => landPlotOptions;

        /// <summary>
        /// The events for the cloud data record.
        /// </summary>
        public EventCallbacks Events => events;

        /// <summary>
        /// The record template ID. This is the ID of the record template that the data will be saved to.
        /// </summary>
        public Guid? RecordTemplateId
        {
            get => Guid.TryParse(recordTemplate, out Guid recordTemplateId) ? recordTemplateId : (Guid?)null;
            set => recordTemplate = value?.ToString();
        }

        /// <summary>
        /// The key of the data. This is the key that the data will be saved to.
        /// </summary>
        public string Key
        {
            get => key;
            set => key = value;
        }

        /// <summary>
        /// Whether or not to display errors when saving data.
        /// </summary>
        public string RecordTemplateIdString
        {
            get => recordTemplate;
            set => recordTemplate = value;
        }

        /// <summary>
        /// Gets or sets the blockchain source.
        /// </summary>
        public string BlockchainSource
        {
            get => blockchain.blockchainSource;
            set => blockchain.blockchainSource = value;
        }

        /// <summary>
        /// Gets or sets the blockchain type.
        /// </summary>
        public int BlockchainType
        {
            get => (int)blockchain.blockchainType;
            set => blockchain.blockchainType = (BlockchainType)value;
        }

        /// <summary>
        /// Whether or not to display errors when saving data.
        /// </summary>
        public bool LoadOnStart
        {
            get => loadOnStart;
            set => loadOnStart = value;
        }

        private static readonly Dictionary<string, CloudDataRecordDto> OfflineData = new();
        private string OfflineDataKey => recordTemplate + key;

        private void Start()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                if (loadOnStart)
                    Load();

                _hasStarted = true;
            });
        }

        /// <summary>
        /// Performs a save operation.
        /// </summary>
        public void Save()
        {
            if (!_hasStarted)
                return;

            if (!MetaverseProgram.IsCoreApp)
            {
                var form = new CloudDataRecordUpsertForm();
                WriteData(form);
                OfflineData[OfflineDataKey] = new CloudDataRecordDto
                {
                    Id = Guid.NewGuid(),
                    StringValue = form.StringValue,
                    BoolValue = form.BoolValue,
                    NumberValue = form.NumberValue,
                    Binary = form.BinaryValue,
                    Key = form.Key,
                    TemplateId = form.TemplateId,
                    UserId = MetaverseProgram.ApiClient.Account.CurrentUser?.Id,
                    MetaSpaceId = form.MetaSpaceId,
                    BlockchainSource = form.BlockchainSource,
                    BlockchainType = form.BlockchainType,
                    UpdatedDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                };
                events.onSaveSuccess?.Invoke();
                events.onSaveFinished?.Invoke();
                return;
            }

            SaveInternal();
        }

        partial void SaveInternal();

        /// <summary>
        /// Performs a load operation.
        /// </summary>
        public void Load()
        {
            if (!MetaverseProgram.IsCoreApp)
            {
                if (OfflineData.TryGetValue(OfflineDataKey, out var dto))
                {
                    events.onLoadFailed?.Invoke();
                    events.onLoadFinished?.Invoke();
                    return;
                }

                var output = ParseData(new CloudDataRecordDto());
                if (output != null)
                {
                    events.onLoadSuccess?.Invoke();
                    events.onLoadFinished?.Invoke();
                    return;
                }

                events.onLoadFailed?.Invoke();
                events.onLoadFinished?.Invoke();
                return;
            }

            LoadInternal();
        }

        partial void LoadInternal();

        /// <summary>
        /// Deletes the data.
        /// </summary>
        public void Delete() => DeleteInternal();

        partial void DeleteInternal();

        /// <summary>
        /// Parses the data into the output type.
        /// </summary>
        /// <param name="record">The record to parse.</param>
        /// <returns>The parsed data.</returns>
        public abstract T ParseData(CloudDataRecordDto record);

        /// <summary>
        /// Writes the data to the form.
        /// </summary>
        /// <param name="form">The form to write to.</param>
        public abstract void WriteData(CloudDataRecordUpsertForm form);

        /// <summary>
        /// Notifies that the land plot has deleted this object.
        /// </summary>
        /// <param name="plot"></param>
        public void NotifyLandPlotDeleted(LandPlot plot)
        {
            if (!plot.IsAllowedToBuild || !landPlotOptions.deleteOnBuildableRemoved)
                return;
            Delete();
        }
    }
}