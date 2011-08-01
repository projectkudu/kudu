using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Kudu.Core.Editor;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;

namespace Kudu.Web.Infrastructure {
    public interface ISiteConfiguration {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }
        string Slug { get; }
        Guid UniqueId { get; }

        IFileSystem FileSystem { get; }
        IDeploymentManager DeploymentManager { get; }
        IRepositoryManager RepositoryManager { get; }
        IRepository Repository { get; }
    }
}