using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Script.Serialization;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Kudu.Web.Hubs.SourceControl.Model;
using Kudu.Web.Models;
using SignalR;
using SignalR.Hubs;

namespace Kudu.Web.Infrastructure {
    public class SiteConfiguration : ISiteConfiguration {
        private static readonly Dictionary<Guid, ISiteConfiguration> _cache = new Dictionary<Guid, ISiteConfiguration>();

        public SiteConfiguration(HttpContextBase httpContext) {
            var serializer = new JavaScriptSerializer();
            var request = serializer.Deserialize<HubRequest>(httpContext.Request["data"]);

            using (var db = new KuduContext()) {
                var application = db.Applications.Find(request.State.AppName);
                ServiceUrl = application.ServiceUrl;
                SiteUrl = application.SiteUrl;
                Name = application.Name;
                Slug = application.Slug;
                UniqueId = application.UniqueId;

                ISiteConfiguration config;
                if (_cache.TryGetValue(UniqueId, out config)) {
                    Repository = config.Repository;
                    FileSystem = config.FileSystem;
                    DeploymentManager = config.DeploymentManager;
                    RepositoryManager = config.RepositoryManager;
                }
                else {
                    Repository = new RemoteRepository(ServiceUrl + "scm");
                    FileSystem = new RemoteFileSystem(ServiceUrl + "files");
                    DeploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
                    RepositoryManager = new RemoteRepositoryManager(ServiceUrl + "scm");

                    DeploymentManager.StatusChanged += OnDeploymentStatusChanged;

                    _cache[UniqueId] = this;
                }
            }
        }

        private void OnDeploymentStatusChanged(DeployResult result) {
            // HACK: This is kinda hacky for now since Signalr doesn't have good support
            // for sending messages through hubs from arbitrary code. I promise it will get better
            var connection = Connection.GetConnection<HubDispatcher>();
            ClientAgent.Invoke(connection,
                               "updateDeployStatus",
                               typeof(SourceControl).FullName,
                               "updateDeployStatus",
                               new[] { new DeployResultViewModel(result) });
        }

        public string Name { get; private set; }
        public string ServiceUrl { get; private set; }
        public string SiteUrl { get; private set; }
        public string Slug { get; private set; }
        public Guid UniqueId { get; private set; }

        public IFileSystem FileSystem {
            get;
            private set;
        }

        public IDeploymentManager DeploymentManager {
            get;
            private set;
        }

        public IRepositoryManager RepositoryManager {
            get;
            private set;
        }

        public IRepository Repository {
            get;
            private set;
        }

        private class HubRequest {
            public ClientState State { get; set; }
        }

        private class ClientState {
            public string AppName { get; set; }
        }
    }
}