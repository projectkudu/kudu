using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.Core.Hooks
{
    public interface IWebHooksManager
    {
        WebHook AddWebHook(WebHook webHook);

        void RemoveWebHook(string hookId);

        IEnumerable<WebHook> WebHooks { get; }

        WebHook GetWebHook(string hookId);

        Task PublishEventAsync(string hookEventType, object eventContent);
    }
}