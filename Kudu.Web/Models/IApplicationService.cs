using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        Task AddApplication(string name);
        Task<bool> DeleteApplication(string name);
        IEnumerable<string> GetApplications();
        IApplication GetApplication(string name);
        bool RemoveLiveSiteBinding(string name, string siteBinding);
        bool RemoveServiceSiteBinding(string name, string siteBinding);
        bool AddSiteBinding(string name, KuduBinding binding);
    }
}
