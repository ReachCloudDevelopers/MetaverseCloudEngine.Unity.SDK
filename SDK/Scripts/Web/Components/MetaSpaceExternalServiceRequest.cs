using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Web.Components
{
    /// <summary>
    /// A component that can be added to a GameObject to make a request to an external service.
    /// </summary>
    [HideMonoScript]
    public class MetaSpaceExternalServiceRequest : TriInspectorMonoBehaviour
    {
        // ReSharper disable InconsistentNaming
        public enum ServiceRequestType
        {
            GET,
            POST,
            PUT,
            DELETE
        }
        // ReSharper restore InconsistentNaming
        
        [Tooltip("If true, the request will be made when the component starts.")]
        [SerializeField] private bool requestOnStart;
        [Tooltip("The URI of the external service to call.")]
        [SerializeField] private string uri = "https://your.api.com";
        [Tooltip("The type of request to make.")]
        [SerializeField] private ServiceRequestType requestType;
        
        [Header("Response")]
        [SerializeField] private UnityEvent<string> onSuccess;
        [SerializeField] private UnityEvent<string> onFailure;

        private bool _started;
        
        /// <summary>
        /// The body of the request.
        /// </summary>
        public string RequestBody { get; set; }

        /// <summary>
        /// The media type of the request body. Defaults to "application/json".
        /// </summary>
        public string MediaType { get; set; } = "application/json";
        
        /// <summary>
        /// The URI of the external service to call.
        /// </summary>
        public string Uri
        {
            get => uri;
            set => uri = value;
        }
        
        /// <summary>
        /// The type of request to make.
        /// </summary>
        public ServiceRequestType RequestType
        {
            get => requestType;
            set => requestType = value;
        }

        private void Start()
        {
            if (requestOnStart) MakeRequest();
            _started = true;
        }

        /// <summary>
        /// Sends the request to the external service.
        /// </summary>
        public void MakeRequest()
        {
            if (requestOnStart && !_started)
                return;
            
            if (!isActiveAndEnabled)
                return;
            
            MetaSpace.OnReady(r =>
            {
                if (!this)
                    return;
                
                switch (requestType)
                {
                    case ServiceRequestType.GET:
                        r.ExternalService.Get(uri, HandleResponse);
                        break;
                    case ServiceRequestType.POST:
                        r.ExternalService.Post(uri, RequestBody, Uri, HandleResponse);
                        break;
                    case ServiceRequestType.PUT:
                        r.ExternalService.Put(uri, RequestBody, Uri, HandleResponse);
                        break;
                    case ServiceRequestType.DELETE:
                        r.ExternalService.Delete(Uri, HandleResponse);
                        break;
                    default:
                        onFailure?.Invoke("Invalid request type.");
                        break;
                }
            });
        }

        private void HandleResponse(MetaSpaceExternalServiceResponse res)
        {
            if (!this) return;
            if (res.IsSuccess) onSuccess?.Invoke(res.Content ?? res.Message);
            else onFailure?.Invoke(res.ErrorMessage ?? res.Message);
        }
    }
}