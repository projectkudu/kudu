using System;
using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplication
    {
        string Name { get; set; }
        string ServiceUrl { get; set; }
        string SiteUrl { get; set; }
        IList<string> SiteUrls { get; set; }
        string DevSiteUrl { get; set; }
    }
}
