using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Kudu.Web.Infrastructure {
    public interface ISiteConfiguration {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }
        string Slug { get; }
    }
}