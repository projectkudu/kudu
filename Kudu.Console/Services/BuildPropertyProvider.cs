using System.Collections.Generic;
using Kudu.Core.Deployment;

namespace Kudu.Services
{
    public class BuildPropertyProvider : IBuildPropertyProvider
    {
        private static readonly Dictionary<string, string> _nullProperties = new Dictionary<string, string>(0);

        public IDictionary<string, string> GetProperties()
        {
            return _nullProperties;
        }
    }
}
