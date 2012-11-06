using System.Collections.Generic;
using System.Linq;

namespace Kudu.SiteManagement
{
    public class Site
    {
        public Site()
        {
            SiteUrls = new List<string>();
        }
        public string ServiceUrl { get; set; }
        public string SiteUrl
        {
            get
            {
                return SiteUrls.FirstOrDefault();
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