using System.Collections.Generic;

namespace Kudu.Core.Jobs
{
    public class PythonScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".py" };

        public PythonScriptHost()
            : base("python.exe")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}