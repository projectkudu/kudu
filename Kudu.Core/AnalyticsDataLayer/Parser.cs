using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsDataLayer
{
    public abstract class Parser
    {
        public abstract bool IsCapable { get; }
    }
}
