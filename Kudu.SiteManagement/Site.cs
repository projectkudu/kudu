using System.Collections.Generic;
using System.Linq;

namespace Kudu.SiteManagement
{
    public class Site
    {
        public Site()
        {
            SiteBindings = new List<KuduBinding>();
            ServiceBindings = new List<KuduBinding>();
        }

        public KuduBinding PrimarySiteBinding
        {
            get
            {
                return SiteBindings.FirstOrDefault();
            }
        }

        public KuduBinding PrimaryServiceBinding
        {
            get
            {
                return ServiceBindings.FirstOrDefault();
            }
        }

        public IList<KuduBinding> SiteBindings { get; set; }
        public IList<KuduBinding> ServiceBindings { get; set; }
    }
}