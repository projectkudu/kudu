using System.Collections.Generic;
using Kudu.Core.Deployment;

namespace Kudu.Services.Web.Services
{
    public class NullBuildPropertyProvider : IBuildPropertyProvider
    {
        private static readonly Dictionary<string, string> _empty = new Dictionary<string, string>();

        public static readonly NullBuildPropertyProvider Instance = new NullBuildPropertyProvider();

        public IDictionary<string, string> GetProperties()
        {
            return _empty;
        }
    }
}