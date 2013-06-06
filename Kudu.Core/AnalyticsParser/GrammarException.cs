using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{
    public class GrammarException:Exception
    {
        public GrammarException(string message):base(message)
        {
        }

        public override string ToString()
        {
            return "GrammerException: " + base.Message;
        }
    }
}
