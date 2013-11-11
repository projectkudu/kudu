using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Kudu.Core.Jobs
{
    public class PhpScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".php" };

        public PhpScriptHost()
            : base(DiscoverHostPath())
        {
        }

        private static string DiscoverHostPath()
        {
            string phpExePath = null;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\PHP"))
            {
                if (key != null)
                {
                    phpExePath = Path.Combine((string)key.GetValue("InstallDir"), "php.exe");
                }
            }

            return phpExePath;
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}