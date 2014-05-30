using System.Collections.Generic;

namespace Kudu.Core.Jobs
{
    public class PowerShellScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".ps1" };

        public PowerShellScriptHost()
            : base("PowerShell.exe", "-ExecutionPolicy RemoteSigned -File {0}{1}")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}
