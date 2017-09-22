
using Kudu.Core.Deployment;

namespace Kudu.Services.FetchHelpers
{
    public class OneDriveInfo : DeploymentInfo
    {
        public string AccessToken { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
    }
}
