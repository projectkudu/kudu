using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Client.Models {
    public interface IApplication {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }
    }
}
