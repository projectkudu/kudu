using System.Collections.Generic;

namespace Kudu.Core.Deployment
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
