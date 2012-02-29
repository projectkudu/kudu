using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        void AddApplication(string name);
        bool DeleteApplication(string name);
        IEnumerable<string> GetApplications();
        IApplication GetApplication(string name);
    }
}
