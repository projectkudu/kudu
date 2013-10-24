using System.Collections.Generic;

namespace Kudu.Core.Jobs
{
    public class BashScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".sh" };

        public BashScriptHost()
            : base("bash")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}