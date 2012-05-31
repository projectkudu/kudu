using System;

namespace Kudu.Web.Models
{
    public interface IApplication
    {
        string Name { get; set; }
        string ServiceUrl { get; set; }
        string SiteUrl { get; set; }
        string DevSiteUrl { get; set; }
    }
}
