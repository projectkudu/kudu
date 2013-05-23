using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        void AddApplication(string name);
        bool DeleteApplication(string name);
        IEnumerable<string> GetApplications();
        IApplication GetApplication(string name);
        bool AddLiveSiteBinding(string name, string siteBinding);
        bool RemoveLiveSiteBinding(string name, string siteBinding);
        bool AddServiceSiteBinding(string name, string siteBinding);
        bool RemoveServiceSiteBinding(string name, string siteBinding);
    }
}
