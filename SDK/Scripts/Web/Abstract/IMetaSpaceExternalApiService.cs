using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Services.Abstract;
using System;

namespace MetaverseCloudEngine.Unity.Web.Abstract
{
    /// <summary>
    /// This interface is used to send requests to external services.
    /// </summary>
    public interface IMetaSpaceExternalApiService : IMetaSpaceService
    {
        /// <summary>
        /// Sends a GET request to an external service.
        /// </summary>
        /// <param name="uri">The URI to send the data to.</param>
        /// <param name="callback">The callback to invoke when the request is complete.</param>
        void Get(string uri, Action<MetaSpaceExternalServiceResponse> callback);
        
        /// <summary>
        /// Sends a PUT request to an external service.
        /// </summary>
        /// <param name="uri">The URI to send the data to.</param>
        /// <param name="body">The body of the request.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <param name="callback">The callback to invoke when the request is complete.</param>
        void Put(string uri, string body, string contentType, Action<MetaSpaceExternalServiceResponse> callback);

        /// <summary>
        /// Sends a POST request to an external service.
        /// </summary>
        /// <param name="uri">The URI to send the data to.</param>
        /// <param name="body">The body of the request.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <param name="callback">The callback to invoke when the request is complete.</param>
        void Post(string uri, string body, string contentType, Action<MetaSpaceExternalServiceResponse> callback);

        /// <summary>
        /// Sends a DELETE request to an external service.
        /// </summary>
        /// <param name="uri">The URI to send the data to.</param>
        /// <param name="callback">The callback to invoke when the request is complete.</param>
        void Delete(string uri, Action<MetaSpaceExternalServiceResponse> callback);
    }
}
