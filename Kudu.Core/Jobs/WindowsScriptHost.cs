using System.Collections.Generic;

namespace Kudu.Core.Jobs
{
    public class WindowsScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".cmd", ".bat", ".exe" };

        public WindowsScriptHost()
            : base("cmd", "/c {0}")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}