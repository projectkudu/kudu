using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Core.AnalyticsDataLayer;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public abstract class MetricRequirement
    {
        protected string LogFormat { get; set; }
        //Fields required in the computation for that metric
        //public abstract LogFields[] GetRequiredFields { get; set; }
    }
}
