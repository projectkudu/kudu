using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public class MetricFormatException: Exception
    {

        public MetricFormatException(string msg): base(msg)
        {
        }


        public override string ToString()
        {
            return "MetricFormatException " + base.ToString();
        }
    }
}
