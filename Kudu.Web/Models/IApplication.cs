using System;

namespace Kudu.Web.Models
{
    public interface IApplication
    {
        DateTime Created { get; set; }
        string Name { get; set; }
        string ServiceUrl { get; set; }
        string SiteUrl { get; set; }
    }
}
