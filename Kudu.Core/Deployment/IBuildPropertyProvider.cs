using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IBuildPropertyProvider
    {
        IDictionary<string, string> GetProperties();
    }
}
