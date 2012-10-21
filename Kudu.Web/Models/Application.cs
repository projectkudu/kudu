using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class Application : IApplication
    {
        public Application()
        {
            SiteUrls = new List<string>();
        }

        public string Name { get; set; }
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