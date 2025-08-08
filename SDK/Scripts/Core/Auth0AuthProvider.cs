using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Common.Models.Forms;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    public class Auth0AuthProvider
    {
        private readonly CancellationToken _cancellationToken;

        public void Start(Action finished, Action failed)
        {
            UniTask.Void(async () =>
            {
                Guid? organizationId = null;
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                organizationId = MetaverseProgram.RuntimeServices?.InternalOrganizationManager?.SelectedOrganization?.Id;
#endif
                var startRequest = await MetaverseProgram.ApiClient.Account.StartAuth0SignInAsync(
                    organizationId);
                if (!startRequest.Succeeded)
                {
                    failed?.Invoke();
                    return;
                }

                await UniTask.Delay(5, cancellationToken: _cancellationToken);
                
                var startResponse = await startRequest.GetResultAsync();
                Application.OpenURL(startResponse.SignInUrl);
                var endRequest = await MetaverseProgram.ApiClient.Account.CompleteAuth0SignInAsync(
                    new GenerateSystemUserTokenAuth0Form
                    {
                        RequestToken = startResponse.RequestToken,
                    }, cancellationToken: _cancellationToken);
                Debug.Log("Done: " + endRequest.Succeeded);
                finished?.Invoke();
            });
        }
        
        public void Cancel()
        {
            
        }
    }
}