using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public class MetricArgument
    {
        public string Parameters { get; set; }
        public override string ToString()
        {
            return Parameters;
        }
    }
}
