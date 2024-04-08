using System.Threading.Tasks;
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
            InitializeInternal(ref internalInitTask);
            if (internalInitTask != null)
                await internalInitTask;
        }

        partial void InitializeInternal(ref Task task);
    }
}
