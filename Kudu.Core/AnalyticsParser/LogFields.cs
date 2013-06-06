using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{
    abstract class LogFields
    {
        protected string logField = string.Empty;
        public abstract string Field { get; }
    }
}
