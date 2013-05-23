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
        public string ServiceUrl
        {
            get
            {
                return ServiceUrls[0];
            }
        }
        public string SiteUrl
        {
            get
            {
                return SiteUrls[0];
            }
        }
        public IList<string> SiteUrls { get; set; }
        public IList<string> ServiceUrls { get; set; }
    }
}