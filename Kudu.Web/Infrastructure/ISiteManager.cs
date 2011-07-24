using Kudu.Web.Models;

namespace Kudu.Web.Infrastructure {
    public interface ISiteManager {
        Site CreateSite(string name);
        void DeleteSite(Application app);
    }
}
