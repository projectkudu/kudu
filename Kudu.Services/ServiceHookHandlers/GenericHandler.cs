using System;
using System.Web;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Generic Servicehook Handler, uses simplest format.
    /// </summary>
    public class GenericHandler : ServiceHookHandlerBase
    {
        // { 
        //   'format':'basic'
        //   'url':'http://host/repository',
        //   'is_hg':true // optional
        // }
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
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

            deploymentInfo = new DeploymentInfo();
            deploymentInfo.RepositoryUrl = url;
            deploymentInfo.RepositoryType = is_hg ? RepositoryType.Mercurial : RepositoryType.Git;
            deploymentInfo.Deployer = GetDeployerFromUrl(url);

            return DeployAction.ProcessDeployment;
        }
    }
}