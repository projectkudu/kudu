using System.Collections.Concurrent;
using Kudu.Client.Model;
using Kudu.Client.Models;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using SignalR.Hubs;

namespace Kudu.Client.Infrastructure {
    public class SiteConfiguration : ISiteConfiguration {
        private static readonly ConcurrentDictionary<string, SiteConfiguration> _cache = new ConcurrentDictionary<string, SiteConfiguration>();

        public SiteConfiguration(IApplication application) {
            ServiceUrl = application.ServiceUrl;
            SiteUrl = application.SiteUrl;
            Name = application.Name;

            SiteConfiguration config;
            if (_cache.TryGetValue(Name, out config)) {
                Repository = config.Repository;
                FileSystem = config.FileSystem;
                RepositoryManager = config.RepositoryManager;
                CommandExecutor = config.CommandExecutor;

                if (config.DeploymentManager.IsActive) {
                    DeploymentManager = config.DeploymentManager;
                }
                else {
                    DeploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
                }
            }
            else {
                Repository = new RemoteRepository(ServiceUrl + "scm");
                FileSystem = new RemoteFileSystem(ServiceUrl + "files");
                DeploymentManager = new RemoteDeploymentManager(ServiceUrl + "deploy");
                RepositoryManager = new RemoteRepositoryManager(ServiceUrl + "scm");
                CommandExecutor = new RemoteCommandExecutor(ServiceUrl + "command");
                DeploymentManager.StatusChanged += OnDeploymentStatusChanged;

                _cache[Name] = this;
            }
        }

        private void OnDeploymentStatusChanged(DeployResult result) {
            var clients = Hub.GetClients<SourceControl>();
            clients.updateDeployStatus(new DeployResultViewModel(result));
        }

        public string Name { get; private set; }
        public string ServiceUrl { get; private set; }
        public string SiteUrl { get; private set; }

        public IEditorFileSystem FileSystem {
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

        IDeploymentManager ISiteConfiguration.DeploymentManager {
            get {
                return DeploymentManager;
            }
        }

        private RemoteDeploymentManager DeploymentManager {
            get;
            set;
        }

        public ICommandExecutor CommandExecutor {
            get;
            private set;
        }
    }
}