using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsDataLayer
{
    public abstract class LogFields
    {
        protected abstract string LogField { get; set; }
        public abstract string Field { get; }
    }
}
