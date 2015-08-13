using System.Collections.Generic;
using Kudu.Contracts.Jobs;
using System.Linq;

namespace Kudu.Core.Jobs
{
    public abstract class ScriptHostBase : IScriptHost
    {
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="hostPath">path to the host running the script</param>
        /// <param name="argumentsFormat">the arguments format passed to the host, {0} is the script file path and {1} are the arguments coming from the user
        /// ({1} if not empty will have an extra whitespace at the beginning before the arguments start)</param>
        protected ScriptHostBase(string hostPath, string argumentsFormat = "{0}{1}")
        {
            HostPath = hostPath;
            ArgumentsFormat = argumentsFormat;
            SupportedExtensions = Enumerable.Empty<string>();
            SupportedFileNames = Enumerable.Empty<string>();
        }

        public string HostPath { get; private set; }

        public string ArgumentsFormat { get; private set; }

        public virtual IEnumerable<string> SupportedExtensions { get; private set; }
        public virtual IEnumerable<string> SupportedFileNames { get; private set; }
    }
}