using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Core.Deployment;

namespace Kudu.Core.Hooks
{
    public interface IWebHooksManager
    {
        void AddWebHook(WebHook webHook);

        void RemoveWebHook(string hookAddress);

        IEnumerable<WebHook> WebHooks { get; }

        Task PublishPostDeploymentAsync(IDeploymentStatusFile statusFile);
    }
}
