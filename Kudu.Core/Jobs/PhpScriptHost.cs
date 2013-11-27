using System.Collections.Generic;

namespace Kudu.Core.Jobs
{
    public class PhpScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".php" };

        public PhpScriptHost()
            : base("php.exe")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}