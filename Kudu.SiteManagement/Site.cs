using System.Collections.Generic;
namespace Kudu.SiteManagement
{
    public class Site
    {
        public Site()
        {
            this.SiteUrls = new List<string>();
        }
        public string ServiceUrl { get; set; }
        public string SiteUrl
        {
            get
            {
                return SiteUrls[0];
            }
            set
            {
                if (SiteUrls.Count > 0)
                {
                    SiteUrls[0] = value;
                }
                else
                {
                    SiteUrls.Add(value);
                }
            }
        }
        public IList<string> SiteUrls { get; set; }
        public string DevSiteUrl { get; set; }
    }
}