namespace Kudu.Web.Infrastructure
{
    public class KuduEnvironment
    {
        public bool RunningAgainstLocalKuduService { get; set; }
        public bool IsAdmin { get; set; }
        public string ServiceSitePath { get; set; }
        public string SitesPath { get; set; }
    }
}