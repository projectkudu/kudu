using System;
using System.Globalization;
using System.Threading.Tasks;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    public abstract class ServiceHookHandlerBase :  IServiceHookHandler
    {
        private static readonly Task _completed = Task.FromResult(0);

        public abstract DeployAction TryParseDeploymentInfo(System.Web.HttpRequestBase request, Newtonsoft.Json.Linq.JObject payload, string targetBranch, out DeploymentInfo deploymentInfo);

        public Task Fetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch, ILogger logger)
        {
            repository.FetchWithoutConflict(deploymentInfo.RepositoryUrl, targetBranch);
            return _completed;
        }

        protected static string GetDeployerFromUrl(string url)
        {
            string host;
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                host = uri.Host;
                if (String.IsNullOrEmpty(host))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_InvalidRepoUrl, url));
                }
            }
            else
            {
                // extract host from git@host:user/repo
                int at = url.IndexOf("@", StringComparison.Ordinal);
                int colon = url.IndexOf(":", StringComparison.Ordinal);
                if (at <= 0 || colon <= 0 || at >= colon)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_InvalidRepoUrl, url));
                }

                host = url.Substring(at + 1, colon - at - 1);
            }

            if (host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
            {
                return "GitHub";
            }

            if (host.EndsWith("bitbucket.org", StringComparison.OrdinalIgnoreCase))
            {
                return "Bitbucket";
            }

            if (host.EndsWith("codeplex.com", StringComparison.OrdinalIgnoreCase))
            {
                return "CodePlex";
            }

            if (host.EndsWith("kilnhg.com", StringComparison.OrdinalIgnoreCase))
            {
                return "Kiln";
            }

            if (host.StartsWith("gitlab", StringComparison.OrdinalIgnoreCase))
            { 
                return "GitlabHQ";
            }

            return host;
        }
    }
}
