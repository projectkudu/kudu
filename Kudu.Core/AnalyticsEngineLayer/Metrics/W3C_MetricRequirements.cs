using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Core.AnalyticsDataLayer;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    /// <summary>
    /// For a defined metric, use this class to encapsulate the fields required to compute for that metric.
    /// </summary>
    public class W3C_MetricRequirements:MetricRequirement
    {
        private LogFields[] _logFields;
        public W3C_MetricRequirements()
        {
            LogFormat = AnalyticsDataLayer.LogFormat.W3C_EXTENDED;
        }

        /// <summary>
        /// It will be at the programmers discretion of what data is needed for this metric on this log format
        /// </summary>
        public LogFields[] RequiredFields { 
            get
            {
                return _logFields;
            }

            set
            {
                W3C_ExtendedField[] temp = value as W3C_ExtendedField[];
                if (temp != null)
                {
                    foreach (W3C_ExtendedField field in temp)
                    {

                    }

                    _logFields = value;
                }
            }
        }
    }
}
