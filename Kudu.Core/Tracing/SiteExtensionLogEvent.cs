using System;
using System.Collections.Generic;

namespace Kudu.Core.Tracing
{
    public class SiteExtensionLogEvent : Dictionary<string, object>
    {
        public SiteExtensionLogEvent(string siteExtension, string eventName)
        {
            this["SiteExtension"] = siteExtension;
            this["EventDate"] = DateTime.UtcNow;
            this["EventName"] = eventName;
        }
    }
}