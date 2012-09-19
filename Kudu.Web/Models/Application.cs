using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class Application : IApplication
    {
        public Application()
        {
            this.SiteUrls = new List<string>();
        }

        public string Name { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
        public List<string> SiteUrls { get; set; }
        public string DevSiteUrl { get; set; }
    }
}