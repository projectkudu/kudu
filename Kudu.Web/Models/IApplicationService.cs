using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        void AddApplication(string userName, string name);
        bool DeleteApplication(string userName, string name);
        IEnumerable<string> GetApplications(string userName);
        IApplication GetApplication(string userName, string name);
    }
}
