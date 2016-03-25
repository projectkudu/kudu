using System;
using System.Collections.Generic;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public interface IApplication
    {
        string Name { get; set; }
        KuduBinding PrimarySiteBinding { get; }
        KuduBinding PrimaryServiceBinding { get; }

        IList<KuduBinding> SiteBindings { get; set; }
        IList<KuduBinding> ServiceBindings { get; set; }
    }
}
