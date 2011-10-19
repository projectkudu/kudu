using Kudu.Web.Models;

namespace Kudu.Web.Infrastructure {
    public interface ISiteManager {
        Site CreateSite(string applicationName);
        void DeleteSite(string applicationName);
        bool TryCreateDeveloperSite(string applicationName, out string siteUrl);
    }
}
