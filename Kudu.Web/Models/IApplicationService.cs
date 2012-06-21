using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        void AddApplication(string name);
        void AddApplication(string name, string hostname, int port);
        bool DeleteApplication(string name);
        IEnumerable<string> GetApplications();
        IApplication GetApplication(string name);
        void CreateDevelopmentSite(string name);
    }
}
