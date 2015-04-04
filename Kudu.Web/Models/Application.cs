using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public class Application : IApplication
    {
        public Application()
        {
            SiteBindings = new List<KuduBinding>();
            ServiceBindings = new List<KuduBinding>();
        }

        public string Name { get; set; }

        public KuduBinding PrimaryServiceBinding
        {
            get
            {
                return ServiceBindings.First();
            }
        }

        public KuduBinding PrimarySiteBinding
        {
            get
            {
                return SiteBindings.First();
            }
        }

        public IList<KuduBinding> SiteBindings { get; set; }
        public IList<KuduBinding> ServiceBindings { get; set; }
    }
}