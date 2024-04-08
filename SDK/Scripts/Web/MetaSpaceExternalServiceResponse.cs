using MetaverseCloudEngine.Common.Models.DataTransfer;

namespace MetaverseCloudEngine.Unity.Web
{
    public class MetaSpaceExternalServiceResponse : MetaSpaceExternalServiceResponseDto
    {
        public bool IsSuccess => StatusCode >= 200 && this.StatusCode < 300;
        public bool IsError => !IsSuccess;
        public string ErrorMessage { get; internal set; }
    }
}
