using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        Task AddApplication(string name);
        Task<bool> DeleteApplication(string name);
        IEnumerable<string> GetApplications();
        IApplication GetApplication(string name);
    }
}
