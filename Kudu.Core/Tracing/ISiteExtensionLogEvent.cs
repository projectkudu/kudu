using System.Collections.Generic;

namespace Kudu.Core.Tracing
{
    public interface ISiteExtensionLogEvent
    {
        IDictionary<string, object> ToDictionary();
    }
}
