using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Kudu.Web.Infrastructure
{
    public class KuduEnvironment
    {
        public bool RunningAgainstLocalKuduService { get; set; }
        public bool IsAdmin { get; set; }
    }
}