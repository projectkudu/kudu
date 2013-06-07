using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Core.Deployment;

namespace Kudu.Core.Hooks
{
    public interface IWebHooksManager
    {
        WebHook AddWebHook(WebHook webHook);

        void RemoveWebHook(string hookId);

        IEnumerable<WebHook> WebHooks { get; }

        WebHook GetWebHook(string hookId);

        Task PublishPostDeploymentAsync(IDeploymentStatusFile statusFile);
    }
}
