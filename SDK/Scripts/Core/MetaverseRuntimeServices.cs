using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Account.Abstract;
using MetaverseCloudEngine.Unity.Account.Poco;

namespace MetaverseCloudEngine.Unity
{
    public partial class MetaverseRuntimeServices
    {
        private readonly MetaverseClient _client;
        private readonly IPrefs _prefs;

        public MetaverseRuntimeServices(MetaverseClient client, IPrefs prefs)
        {
            _client = client;
            _prefs = prefs;
        }

        public bool UpdateRequired { get; private set; }
        public ILoginStore LoginStore { get; private set; }

        public async Task InitializeAsync()
        {
            LoginStore = new LoginStore(_prefs, _client);
            await LoginStore.InitializeAsync();

            Task internalInitTask = null;
            // ReSharper disable once InvocationIsSkipped
            InitializeInternal(ref internalInitTask);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (internalInitTask != null)
                // ReSharper disable once HeuristicUnreachableCode
                await internalInitTask;
        }

        // ReSharper disable once PartialMethodWithSinglePart
        partial void InitializeInternal(ref Task task);

        [UsedImplicitly]
        public void CheckForUpdates(Action callback = null, bool force = false)
        {
            // ReSharper disable once InvocationIsSkipped
            CheckForUpdatesInternal(callback, force);
        }

        [UsedImplicitly]
        // ReSharper disable once PartialMethodWithSinglePart
        partial void CheckForUpdatesInternal(Action callback = null, bool force = false);
    }
}
