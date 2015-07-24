using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Jobs
{
    class FSharpScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".fsx" };

        public FSharpScriptHost()
            : base("cmd", "/c fsi.exe {0}{1}")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}
