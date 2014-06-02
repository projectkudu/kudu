using System.Collections.Generic;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Jobs
{
    public class NodeScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".js" };

        public NodeScriptHost()
            : base("node.exe")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}