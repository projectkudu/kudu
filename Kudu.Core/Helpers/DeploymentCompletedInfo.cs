using System;
using System.IO;
using System.Web.Script.Serialization;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Helpers
{
    public class DeploymentCompletedInfo
    {
        public const string LatestDeploymentFile = "LatestDeployment.json";

        public string TimeStamp { get; set; }
        public string SiteName { get; set; }
        public string RequestId { get; set; }
        public string Kind { get; set; }
        public string Status { get; set; }
        public string Details { get; set; }

        public static void Persist(string requestId, IDeploymentStatusFile status)
        {
            // signify the deployment is done by git push
            var kind = System.Environment.GetEnvironmentVariable(Constants.ScmDeploymentKind);
            if (string.IsNullOrEmpty(kind))
            {
                kind = status.Deployer;
            }

            var serializer = new JavaScriptSerializer();
            Persist(status.SiteName, kind, requestId, status.Status.ToString(), serializer.Serialize(status));
        }

        public static void Persist(string siteName, string kind, string requestId, string status, string details)
        {
            var info = new DeploymentCompletedInfo
            {
                TimeStamp = $"{DateTime.UtcNow:s}Z",
                SiteName = siteName,
                Kind = kind,
                RequestId = requestId,
                Status = status,
                Details = details ?? string.Empty
            };

            try
            {
                var path = Path.Combine(System.Environment.ExpandEnvironmentVariables(@"%HOME%"), "site", "deployments");
                var file = Path.Combine(path, $"{Constants.LatestDeployment}.json");
                var serializer = new JavaScriptSerializer();
                var content = serializer.Serialize(info);
                FileSystemHelpers.EnsureDirectory(path);

                // write deployment info to %home%\site\deployments\LatestDeployment.json
                OperationManager.Attempt(() => FileSystemHelpers.Instance.File.WriteAllText(file, content));

                // write to etw
                KuduEventSource.Log.DeploymentCompleted(
                    info.SiteName,
                    info.Kind,
                    info.RequestId,
                    info.Status,
                    info.Details);
            }
            catch (Exception ex)
            {
                KuduEventSource.Log.KuduException(
                    info.SiteName,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    $"{ex}");
            }
        }
    }
}
