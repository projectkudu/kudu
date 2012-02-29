using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Client.Infrastructure;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        IApplication AddApplication(string name);
        bool DeleteApplication(string name);
        IEnumerable<IApplication> GetApplications();
        IApplication GetApplication(string name);
    }
}
