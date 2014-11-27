using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement
{
    public struct KuduBinding
    {
        public UriScheme Schema { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Host { get; set; }
        public bool Sni { get; set; }
        public string Certificate { get; set; }
        public SiteType SiteType { get; set; }
    }
}