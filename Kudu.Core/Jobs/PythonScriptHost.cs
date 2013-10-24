using System.Collections.Generic;
using Microsoft.Win32;

namespace Kudu.Core.Jobs
{
    public class PythonScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".py" };

        public PythonScriptHost()
            : base(DiscoverHostPath())
        {
        }

        private static string DiscoverHostPath()
        {
            string pythonExePath = null;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Python.exe"))
            {
                if (key != null)
                {
                    pythonExePath = (string)key.GetValue(null);
                }
            }

            return pythonExePath;
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}