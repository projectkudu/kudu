using System.Collections.Generic;
using Kudu.Contracts.Jobs;

namespace Kudu.Core.Jobs
{
    public abstract class ScriptHostBase : IScriptHost
    {
        protected ScriptHostBase(string hostPath, string argumentsFormat = "{0}")
        {
            HostPath = hostPath;
            ArgumentsFormat = argumentsFormat;
        }

        public string HostPath { get; private set; }

        public string ArgumentsFormat { get; private set; }

        public abstract IEnumerable<string> SupportedExtensions { get; }
    }
}