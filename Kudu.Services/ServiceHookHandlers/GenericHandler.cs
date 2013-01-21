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
        //   'url':'http://host/repository',
        //   'is_hg':true // optional
        // }
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;

            string url = null;
            bool? is_hg = null;
            foreach (var pair in payload)
            {
                if (pair.Key == "url" && url == null)
                {
                    url = (string)pair.Value;
                }
                else if (pair.Key == "is_hg" && is_hg == null)
                {
                    is_hg = (bool)pair.Value;
                }
                else
                {
                    // Unknown property
                    return DeployAction.UnknownPayload;
                }
            }

            if (String.IsNullOrEmpty(url))
            {
                return DeployAction.UnknownPayload;
            }

            var info = new DeploymentInfo();
            info.RepositoryUrl = url;

            if (is_hg != null)
            {
                info.RepositoryType = is_hg.Value ? RepositoryType.Mercurial : RepositoryType.Git;
            }
            else
            {
                info.RepositoryType = url.StartsWith("hg@", StringComparison.OrdinalIgnoreCase) ? RepositoryType.Mercurial : RepositoryType.Git;
            }

            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                info.Deployer = uri.Host;
            }
            else
            {
                int at = url.IndexOf("@");
                int colon = url.IndexOf(":");
                if (at <= 0 || colon <= 0 || at >= colon)
                {
                    return DeployAction.UnknownPayload;
                }

                info.Deployer = url.Substring(at + 1, colon - at - 1);
            }

            deploymentInfo = info;
            return DeployAction.ProcessDeployment;
        }
    }
}