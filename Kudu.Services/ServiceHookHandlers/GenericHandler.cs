using System;
using System.Web;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Generic Servicehook Handler, uses simplest format.
    /// </summary>
    public class GenericHandler : ServiceHookHandlerBase
    {
        public GenericHandler(IRepositoryFactory repositoryFactory)
            : base(repositoryFactory)
        {
        }

        // { 
        //   'format':'basic'
        //   'url':'http://host/repository',
        //   'is_hg':true // optional
        // }
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase deploymentInfo)
        {
            deploymentInfo = null;
            if (!String.Equals(payload.Value<string>("format"), "basic", StringComparison.OrdinalIgnoreCase))
            {
                return DeployAction.UnknownPayload;
            }

            string url = payload.Value<string>("url");
            if (String.IsNullOrEmpty(url))
            {
                return DeployAction.UnknownPayload;
            }

            string scm = payload.Value<string>("scm");
            bool is_hg;
            if (String.IsNullOrEmpty(scm))
            {
                // SSH hg@... vs git@...
                is_hg = url.StartsWith("hg@", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                is_hg = String.Equals(scm, "hg", StringComparison.OrdinalIgnoreCase);
            }

            deploymentInfo = new DeploymentInfo(RepositoryFactory);
            SetRepositoryUrl(deploymentInfo, url);
            deploymentInfo.RepositoryType = is_hg ? RepositoryType.Mercurial : RepositoryType.Git;
            deploymentInfo.Deployer = GetDeployerFromUrl(url);
            deploymentInfo.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Fetch from " + url);
            deploymentInfo.AllowDeploymentWhileScmDisabled = true;

            return DeployAction.ProcessDeployment;
        }

        public static void SetRepositoryUrl(DeploymentInfoBase deploymentInfo, string url)
        {
            string commitId = null;
            int index = url.LastIndexOf('#');
            if (index >= 0)
            {
                if (index + 1 < url.Length)
                {
                    commitId = url.Substring(index + 1);
                }
                url = url.Substring(0, index);
            }

            deploymentInfo.RepositoryUrl = url;
            deploymentInfo.CommitId = commitId;
        }
    }
}