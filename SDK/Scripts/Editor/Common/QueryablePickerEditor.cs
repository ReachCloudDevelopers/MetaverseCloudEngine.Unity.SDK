using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetaverseCloudEngine.Unity.Async;

namespace MetaverseCloudEngine.Unity.Editors
{
    public abstract class QueryablePickerEditor<TEntity, TQueryParams> : PickerEditor
    {
        private bool _requestingPickablesAsync;

        protected abstract Task<IEnumerable<TEntity>> QueryAsync(TQueryParams queryParams);

        protected abstract TQueryParams GetQueryParams(int count, int offset, string filter);

        protected override bool RequestPickables(int offset, int count, string filter)
        {
            if (!MetaverseProgram.Initialized)
            {
                Close();
                return false;
            }

            if (!MetaverseProgram.ApiClient.Account.IsLoggedIn)
            {
                Close();
                MetaverseAccountWindow.LoginRequired();
                return false;
            }

            if (_requestingPickablesAsync)
                return true;

            var queryParams = GetQueryParams(count, offset, filter);
            QueryAsync(queryParams).Then(x =>
            {
                _requestingPickablesAsync = false;
                OnPickablesReceived(x.Select(x => (object)x).ToArray());

            }, OnQueryError);

            return _requestingPickablesAsync = true;
        }

        protected void OnQueryError(object e)
        {
            Error = e.ToString();
            _requestingPickablesAsync = false;
        }
    }
}
