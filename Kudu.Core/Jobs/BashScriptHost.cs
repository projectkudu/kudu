using System.Collections.Generic;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Jobs
{
    public class BashScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".sh" };

        public BashScriptHost()
            : base(DiscoverHostPath())
        {
        }

        private static string DiscoverHostPath()
        {
            return PathUtilityFactory.Instance.ResolveBashPath();
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}