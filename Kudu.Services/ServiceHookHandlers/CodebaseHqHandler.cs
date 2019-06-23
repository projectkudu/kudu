using System;
using System.Web;
using Newtonsoft.Json.Linq;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    public class CodebaseHqHandler : GitHubCompatHandler
    {
        public CodebaseHqHandler(IRepositoryFactory repositoryFactory)
            : base(repositoryFactory)
        {
        }

        protected override bool ParserMatches(HttpRequestBase request, JObject payload, string targetBranch)
        {
            return (request.UserAgent != null && request.UserAgent.StartsWith("Codebasehq", StringComparison.OrdinalIgnoreCase));
        }

        protected override bool IsNoop(HttpRequestBase request, JObject payload, string targetBranch)
        {
            // FIXME if githubcompathandler failed to parse the body => NOOP
            return !(base.ParserMatches(request, payload, targetBranch)) || base.IsNoop(request, payload, targetBranch);
        }

        protected override string DetermineSecurityProtocol(JObject payload)
        {
            // CodebaseHq format, see http://support.codebasehq.com/kb/howtos/repository-push-commit-notifications
            var repository = payload.Value<JObject>("repository");
            var urls = repository.Value<JObject>("clone_urls");
            var isPrivate = repository.Value<bool>("private");

            return isPrivate ? urls.Value<string>("ssh") : urls.Value<string>("http");
        }

        protected override string GetDeployer()
        {
            return "CodebaseHQ";
        }
    }
}