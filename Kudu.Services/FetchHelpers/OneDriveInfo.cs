using Kudu.Services.ServiceHookHandlers;

namespace Kudu.Services.FetchHelpers
{
    public class OneDriveInfo : DeploymentInfo
    {
        public string AccessToken { get; set; }
    }
}
