namespace Kudu.Core.Tracing
{
    public class KuduSiteExtensionLogEvent : SiteExtensionLogEvent
    {
        public KuduSiteExtensionLogEvent(string eventName)
            : base("Kudu", eventName)
        {
        }
    }
}