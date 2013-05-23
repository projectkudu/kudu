using System;
using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplication
    {
        string Name { get; set; }
        string ServiceUrl { get; }
        string SiteUrl { get; }
        IList<string> SiteUrls { get; set; }
        IList<string> ServiceUrls { get; set; }
    }
}
