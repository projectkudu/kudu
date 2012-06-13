using System.Collections.Generic;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        void AddApplication(string name);
        void AddApplication(string name, string hostname, int port);
        bool DeleteApplication(string name);
        IEnumerable<string> GetApplications();
        IApplication GetApplication(string name);
        Site GetSite(string name);
        void CreateDevelopmentSite(string name);
    }
}
