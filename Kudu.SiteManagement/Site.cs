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
        public string ServiceUrl
        {
            get
            {
                return ServiceUrls.FirstOrDefault();
            }
        }
        public string SiteUrl
        {
            get
            {
                return SiteUrls.FirstOrDefault();
            }
        }
        public IList<string> SiteUrls { get; set; }
        public IList<string> ServiceUrls { get; set; }
    }
}