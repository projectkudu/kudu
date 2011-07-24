using Kudu.Web.Models;

namespace Kudu.Web.Infrastructure {
    public interface ISiteManager {
        Site CreateSite(string siteName);
        void DeleteSite(string siteName, string applicationName);
    }
}
