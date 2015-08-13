using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Jobs
{
    class DnxScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { "project.json" };

        public DnxScriptHost()
            : base("cmd", "/c runDnxWebJob.cmd")
        {
        }

        public override IEnumerable<string> SupportedFileNames
        {
            get { return Supported; }
        }
    }
}
